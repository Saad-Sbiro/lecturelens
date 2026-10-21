using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text.Json;
using LectureLens.Core;

namespace LectureLens.Storage;

public sealed class LocalStore(string root) : IRecordingStorage, IDisposable
{
    public static readonly JsonSerializerOptions Json = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
    public const int MaxPacketBytes = 524288;
    public string Root { get; } = Path.GetFullPath(root);
    private string? active;
    private FileStream? rootLock;
    private FileStream? sessionLock;
    private int next;
    private double lastMs;
    public static string DefaultRoot => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "LectureLens", "recordings");

    private string Folder(string id)
    {
        if (!Guid.TryParseExact(id, "D", out var parsed) || parsed.ToString() != id) throw new InvalidDataException("Invalid session identifier");
        return Path.Combine(Root, id);
    }
    private static void Write(string file, byte[] bytes)
    {
        var temporary = file + ".partial";
        using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096, FileOptions.WriteThrough))
        { stream.Write(bytes); stream.Flush(true); }
        File.Move(temporary, file);
    }
    private static void WriteJson(string file, object value) => Write(file, JsonSerializer.SerializeToUtf8Bytes(value, Json));
    public string Create(string mime)
    {
        if (mime is not ("video/webm;codecs=vp8,opus" or "video/webm;codecs=vp9,opus" or "video/webm")) throw new InvalidDataException("Unsupported media type");
        if (active != null) throw new InvalidOperationException("A session is already active");
        Directory.CreateDirectory(Root);
        rootLock = new FileStream(Path.Combine(Root, "capture.lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
        try
        {
            var id = Guid.NewGuid().ToString(); var folder = Folder(id); Directory.CreateDirectory(folder);
            sessionLock = new FileStream(Path.Combine(folder, "active.lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
            WriteJson(Path.Combine(folder, "manifest.json"), new { schemaVersion = 1, ownerId = "local", id, mimeType = mime, createdAt = DateTimeOffset.UtcNow.ToString("O"), source = "browser-tab" });
            active = id; next = 0; lastMs = 0; return id;
        }
        catch { Dispose(); throw; }
    }
    public void Append(string id, MediaPacket packet)
    {
        RequireActive(id);
        if (packet.Sequence != next || !double.IsFinite(packet.AtMs) || packet.AtMs < lastMs || packet.Bytes.Length is < 1 or > MaxPacketBytes)
            throw new InvalidDataException("Out-of-order, empty or oversized packet");
        var header = JsonSerializer.SerializeToUtf8Bytes(new { schemaVersion = 1, ownerId = "local", sequence = packet.Sequence, atMs = packet.AtMs, reason = packet.Reason, length = packet.Bytes.Length, sha256 = Convert.ToHexStringLower(SHA256.HashData(packet.Bytes)) }, Json);
        var bytes = new byte[4 + header.Length + packet.Bytes.Length];
        BinaryPrimitives.WriteInt32LittleEndian(bytes, header.Length); header.CopyTo(bytes, 4); packet.Bytes.CopyTo(bytes, 4 + header.Length);
        Write(Path.Combine(Folder(id), $"{next:D8}.chunk"), bytes);
        next++; lastMs = packet.AtMs;
    }
    private void RequireActive(string id) { if (id != active) throw new InvalidOperationException("Session is not active"); }
    public void Event(string id, string type, double atMs, string reason)
    {
        RequireActive(id);
        if (type is not ("started" or "paused" or "resumed" or "ended" or "diagnostic") || !double.IsFinite(atMs) || atMs < 0 || reason.Length > 1000) throw new InvalidDataException("Invalid timeline event");
        using var stream = new FileStream(Path.Combine(Folder(id), "events.ndjson"), FileMode.Append, FileAccess.Write, FileShare.Read);
        stream.Write(JsonSerializer.SerializeToUtf8Bytes(new { schemaVersion = 1, ownerId = "local", type, atMs, reason }, Json));
        stream.WriteByte(10); stream.Flush(true);
    }
    public void Finish(string id, string reason)
    {
        RequireActive(id);
        if (reason.Length > 100) throw new InvalidDataException("Invalid end reason");
        try { WriteJson(Path.Combine(Folder(id), "finished.json"), new { reason, finishedAt = DateTimeOffset.UtcNow.ToString("O") }); }
        finally { Dispose(); }
    }
    public static MediaPacket ReadPacket(string file)
    {
        var size = new FileInfo(file).Length;
        if (size < 5 || size > MaxPacketBytes + 4096) throw new InvalidDataException("Invalid packet size");
        var packet = File.ReadAllBytes(file); var headerSize = BinaryPrimitives.ReadInt32LittleEndian(packet);
        if (headerSize is < 2 or > 4096 || headerSize + 4 > packet.Length) throw new InvalidDataException("Invalid packet header");
        using var doc = JsonDocument.Parse(packet.AsMemory(4, headerSize)); var h = doc.RootElement;
        var bytes = packet[(4 + headerSize)..];
        if (h.GetProperty("length").GetInt32() != bytes.Length || h.GetProperty("sha256").GetString() != Convert.ToHexStringLower(SHA256.HashData(bytes))) throw new InvalidDataException("Packet integrity failure");
        return new MediaPacket(h.GetProperty("sequence").GetInt32(), h.GetProperty("atMs").GetDouble(), bytes, h.GetProperty("reason").GetString()!);
    }
    private bool IsActive(string id)
    {
        if (id == active) return true;
        var file = Path.Combine(Folder(id), "active.lock");
        if (!File.Exists(file)) return false;
        try { using var probe = new FileStream(file, FileMode.Open, FileAccess.ReadWrite, FileShare.None); return false; }
        catch (IOException) { return true; }
    }
    private SessionInfo Scan(string id, Action<MediaPacket>? consume = null)
    {
        var folder = Folder(id);
        using var manifest = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(folder, "manifest.json")));
        var problems = Directory.GetFiles(folder, "*.partial").Select(f => "uncommitted:" + Path.GetFileName(f)).ToList();
        int count = 0; long bytes = 0; double time = 0;
        foreach (var file in Directory.GetFiles(folder, "*.chunk").Order(StringComparer.Ordinal))
        {
            MediaPacket packet;
            try
            {
                packet = ReadPacket(file);
                if (Path.GetFileName(file) != $"{count:D8}.chunk" || packet.Sequence != count || !double.IsFinite(packet.AtMs) || packet.AtMs < time) throw new InvalidDataException("Sequence gap");
            }
            catch (Exception e) when (e is IOException or InvalidDataException or JsonException or InvalidOperationException or KeyNotFoundException)
            { problems.Add("corrupt-or-gap:" + Path.GetFileName(file)); break; }
            consume?.Invoke(packet); count++; bytes += packet.Bytes.Length; time = packet.AtMs;
        }
        using var finished = File.Exists(Path.Combine(folder, "finished.json")) ? JsonDocument.Parse(File.ReadAllBytes(Path.Combine(folder, "finished.json"))) : null;
        var reason = finished?.RootElement.GetProperty("reason").GetString();
        var state = IsActive(id) ? "recording" : problems.Count > 0 || reason == "capture-error" ? "needs-review" : reason != null ? "finished" : "interrupted";
        return new SessionInfo(id, manifest.RootElement.GetProperty("createdAt").GetString()!, state, count, bytes, problems.ToArray(), reason);
    }
    public IReadOnlyList<SessionInfo> List()
    {
        Directory.CreateDirectory(Root); var result = new List<SessionInfo>();
        foreach (var folder in Directory.GetDirectories(Root))
        {
            var id = Path.GetFileName(folder); if (!Guid.TryParseExact(id, "D", out _)) continue;
            try { result.Add(Scan(id)); }
            catch (Exception e) when (e is IOException or InvalidDataException or JsonException or InvalidOperationException or KeyNotFoundException)
            { result.Add(new SessionInfo(id, "", "needs-review", 0, 0, ["unreadable-manifest"])); }
        }
        return result.OrderByDescending(x => x.CreatedAt).ToArray();
    }
    public string Recover(string id)
    {
        if (IsActive(id)) throw new InvalidOperationException("End recording before recovery");
        var target = Path.Combine(Folder(id), "recording.webm"); SessionInfo info;
        using (var stream = new FileStream(target + ".partial", FileMode.Create, FileAccess.Write, FileShare.None))
        { info = Scan(id, p => stream.Write(p.Bytes)); stream.Flush(true); }
        if (info.Chunks == 0 || info.Problems.Any(p => p.StartsWith("corrupt-or-gap:"))) throw new InvalidDataException("Recovery incomplete: originals preserved for review");
        File.Move(target + ".partial", target, true); return target;
    }
    public void RequestStop(string id) { if (!IsActive(id)) throw new InvalidOperationException("Session is not active"); File.WriteAllText(Path.Combine(Folder(id), "stop.request"), "user-stop"); }
    public bool StopRequested(string id) { RequireActive(id); return File.Exists(Path.Combine(Folder(id), "stop.request")); }
    public void Delete(string id)
    {
        if (IsActive(id)) throw new InvalidOperationException("Cannot delete an active session");
        var folder = Folder(id);
        if (Path.GetDirectoryName(folder) != Root || (File.GetAttributes(folder) & FileAttributes.ReparsePoint) != 0) throw new InvalidDataException("Unsafe session path");
        Directory.Delete(folder, true);
    }
    public long FreeBytes() { Directory.CreateDirectory(Root); return new DriveInfo(Path.GetPathRoot(Root)!).AvailableFreeSpace; }
    public void Dispose() { active = null; sessionLock?.Dispose(); sessionLock = null; rootLock?.Dispose(); rootLock = null; }
}
