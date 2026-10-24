using System.Buffers.Binary;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;
using LectureLens.Core;
using LectureLens.Storage;

namespace LectureLens.NativeHost;

internal static partial class Program
{
    [LibraryImport("kernel32.dll")]
    private static partial uint SetThreadExecutionState(uint flags);
    private static int Main(string[] args)
    {
        var test = args.Length == 2 && args[0] == "--test-root";
        var desktop = args.Length == 1 && args[0] == "--desktop-capture";
        if (!test && !desktop && (args.Length == 0 || args[0] != File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "allowed-origin.txt")).Trim())) return 2;
        using var store = new LocalStore(test ? args[1] : LocalStore.DefaultRoot);
        using var input = Console.OpenStandardInput(); using var output = Console.OpenStandardOutput();
        string? active = null; var clock = new Stopwatch();
        try
        {
            while (true)
            {
                var prefix = new byte[4]; var first = input.ReadByte(); if (first < 0) break;
                prefix[0] = (byte)first; input.ReadExactly(prefix.AsSpan(1));
                var length = BinaryPrimitives.ReadInt32LittleEndian(prefix);
                if (length is < 2 or > 1048576) return 3;
                var message = new byte[length]; input.ReadExactly(message);
                using var document = JsonDocument.Parse(message); var request = document.RootElement;
                var requestId = request.GetProperty("requestId").GetString();
                object response;
                try
                {
                    var op = request.GetProperty("op").GetString(); object? value;
                    switch (op)
                    {
                        case "hello": value = new { version = 1, freeBytes = store.FreeBytes() }; break;
                        case "termsStatus":
                            value = new { accepted = new TermsAgreement(store.Root).IsAccepted(), version = TermsAgreement.Document.Version }; break;
                        case "acceptTerms":
                            if (!request.GetProperty("accepted").GetBoolean()) throw new InvalidDataException("Explicit acceptance required.");
                            new TermsAgreement(store.Root).Accept(request.GetProperty("version").GetString()!);
                            value = new { accepted = true, version = TermsAgreement.Document.Version }; break;
                        case "detectRegion" when !test:
                            if (active != null || !request.GetProperty("consent").GetBoolean()) throw new InvalidDataException("Vision needs a separate connection and image consent");
                            value = GeminiVision.Detect(request.GetProperty("data").GetString()!).GetAwaiter().GetResult(); break;
                        case "create":
                            if (!request.GetProperty("consent").GetBoolean()) throw new InvalidDataException("Consent required");
                            if (store.FreeBytes() < 3136870912) throw new IOException("Not enough free disk space for recording and export");
                            active = store.Create(request.GetProperty("mime").GetString()!, desktop ? "desktop-window" : "browser-tab"); clock.Restart();
                            if (OperatingSystem.IsWindows()) SetThreadExecutionState(0x80000003);
                            value = new { id = active }; break;
                        case "append":
                            var id = request.GetProperty("id").GetString()!; if (id != active) throw new InvalidDataException("Wrong active session");
                            store.Append(id, new MediaPacket(request.GetProperty("sequence").GetInt32(), request.GetProperty("atMs").GetDouble(), Convert.FromBase64String(request.GetProperty("data").GetString()!), "capture-chunk"));
                            value = new { sequence = request.GetProperty("sequence").GetInt32() }; break;
                        case "event":
                            store.Event(request.GetProperty("id").GetString()!, request.GetProperty("type").GetString()!, request.GetProperty("atMs").GetDouble(), request.GetProperty("reason").GetString()!);
                            value = true; break;
                        case "slide":
                            store.SaveSlide(request.GetProperty("id").GetString()!, request.GetProperty("slide").GetInt32(), request.GetProperty("atMs").GetDouble(), request.GetProperty("crop"), request.GetProperty("reason").GetString()!, Convert.FromBase64String(request.GetProperty("data").GetString()!));
                            value = true; break;
                        case "visualEvent":
                            store.VisualEvent(request.GetProperty("id").GetString()!, request.GetProperty("atMs").GetDouble(), request.GetProperty("reason").GetString()!, request.TryGetProperty("slide", out var visualSlide) ? visualSlide.GetInt32() : null, request.GetProperty("crop"));
                            value = true; break;
                        case "heartbeat": value = new { stopRequested = active != null && (store.StopRequested(active) || store.FreeBytes() < 268435456), elapsedMs = clock.Elapsed.TotalMilliseconds }; break;
                        case "finish":
                            var finished = request.GetProperty("id").GetString()!;
                            store.Finish(finished, request.GetProperty("reason").GetString()!); active = null;
                            if (OperatingSystem.IsWindows()) SetThreadExecutionState(0x80000000);
                            value = new { path = store.Recover(finished) }; break;
                        case "list" when test: value = store.List(); break;
                        case "recover" when test: value = store.Recover(request.GetProperty("id").GetString()!); break;
                        default: throw new InvalidDataException("Unsupported operation");
                    }
                    response = new { requestId, ok = true, value };
                }
                catch (Exception e) when (e is IOException or InvalidDataException or InvalidOperationException or FormatException or KeyNotFoundException or ArgumentException)
                { response = new { requestId, ok = false, error = e is IOException ? "Local storage unavailable; saved chunks preserved" : e.Message }; }
                var encoded = JsonSerializer.SerializeToUtf8Bytes(response, LocalStore.Json);
                BinaryPrimitives.WriteInt32LittleEndian(prefix, encoded.Length); output.Write(prefix); output.Write(encoded); output.Flush();
            }
            return 0;
        }
        catch (Exception e) when (e is IOException or JsonException or InvalidOperationException or KeyNotFoundException) { return 4; }
        finally { if (OperatingSystem.IsWindows()) SetThreadExecutionState(0x80000000); }
    }
}
