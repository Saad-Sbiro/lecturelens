using System.IO;
using System.Text;
using System.Text.Json;
using LectureLens.Core;
using LectureLens.Storage;

namespace LectureLens.Desktop.Adapters;

// Decode each container independently. Concatenating WebM bytes loses later parts in many players.
internal sealed class RecordingMerge(ILocalSpeechRuntime? decoder = null)
{
    internal async Task<string> Run(string root, IReadOnlyList<string> ids, string title, IProgress<int>? progress, CancellationToken cancel)
    {
        if (ids.Count is < 2 or > 3 || ids.Distinct().Count() != ids.Count) throw new InvalidDataException("Choose two or three different recordings.");
        if (title.Trim().Length is < 1 or > 120 || title.Any(char.IsControl)) throw new InvalidDataException("Use a name of 1–120 characters.");
        using var source = new LocalStore(root);
        var available = source.List().ToDictionary(s => s.Id);
        if (ids.Any(id => !available.TryGetValue(id, out var s) || s.State is not ("finished" or "interrupted") || s.Chunks == 0 || s.Problems.Length > 0))
            throw new InvalidDataException("Choose stopped recordings with intact raw audio.");
        var staging = Path.Combine(source.Root, ".merge-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(staging);
        var locks = new List<FileStream>();
        try
        {
            foreach (var id in ids) {
                var folder = Path.Combine(source.Root, id);
                Safe(folder);
                Safe(Path.Combine(folder, "active.lock"));
                locks.Add(new FileStream(Path.Combine(folder, "active.lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None));
            }
            var runtime = decoder ?? new LocalSpeechProcess();
            using var output = new LocalStore(staging);
            var merged = output.Create("audio/wav");
            var destination = Path.Combine(staging, merged);
            var audio = Path.Combine(destination, "audio.wav");
            var parts = new List<object>();
            var slideNumber = 0;
            long pcmBytes = 0;
            await using (var wav = new FileStream(audio, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None, 81920, true))
            {
                await wav.WriteAsync(new byte[44], cancel);
                for (int index = 0; index < ids.Count; index++)
                {
                    cancel.ThrowIfCancellationRequested(); progress?.Report(index + 1);
                    var id = ids[index]; var folder = Path.Combine(source.Root, id);
                    var input = Path.Combine(staging, $"input-{index}");
                    await Reconstruct(folder, input, available[id].Chunks, cancel);
                    var offsetMs = pcmBytes * 1000.0 / LocalSpeech.BytesPerSecond;
                    long partBytes = 0;
                    await using (var decoded = await runtime.Decode(input, Path.Combine(staging, $"decode-{index}.log"), cancel))
                    {
                        var buffer = new byte[81920]; int count;
                        while ((count = await decoded.Audio.ReadAsync(buffer, cancel)) > 0)
                        {
                            if (pcmBytes + count > uint.MaxValue - 36 || output.FreeBytes() < 268435456 + count) throw new IOException("Not enough space for merged audio.");
                            await wav.WriteAsync(buffer.AsMemory(0, count), cancel); pcmBytes += count; partBytes += count;
                        }
                        await decoded.Complete();
                    }
                    if (partBytes == 0 || partBytes % 2 != 0) throw new InvalidDataException("An input recording has incomplete audio.");
                    var durationMs = partBytes * 1000.0 / LocalSpeech.BytesPerSecond;
                    parts.Add(new { sourceId = id, title = available[id].Title, offsetMs, durationMs });
                    var provenance = Path.Combine(destination, "source-events"); Directory.CreateDirectory(provenance);
                    foreach (var log in new[] { "events.ndjson", "visual-events.ndjson" }) {
                        var original = Path.Combine(folder, log); if (!File.Exists(original)) continue;
                        Safe(original);
                        await using var reader = File.OpenRead(original);
                        await using var writer = File.Create(Path.Combine(provenance, $"{index + 1}-{log}"));
                        await reader.CopyToAsync(writer, cancel);
                    }
                    var slideFolder = Path.Combine(folder, "slides");
                    if (Directory.Exists(slideFolder))
                    {
                        Safe(slideFolder);
                        foreach (var file in Directory.GetFiles(slideFolder, "*.slide.json").Order(StringComparer.Ordinal))
                        {
                            cancel.ThrowIfCancellationRequested(); Safe(file);
                            var validated = await BrowserPdf.ReadSlide(file, cancel);
                            using var slide = JsonDocument.Parse(await File.ReadAllTextAsync(file, cancel));
                            var item = slide.RootElement;
                            var localMs = AudioTime(folder, item.GetProperty("atMs").GetDouble());
                            // MediaRecorder timestamps are approximate; never extend beyond decoded audio.
                            var atMs = offsetMs + Math.Clamp(localMs, 0, durationMs);
                            output.SaveSlide(merged, slideNumber++, atMs, item.GetProperty("crop"), "merged-source-slide", Convert.FromBase64String(validated.Image));
                            output.Event(merged, "diagnostic", atMs, $"source:{id};slide:{validated.Number};originalAtMs:{item.GetProperty("atMs")};reason:{item.GetProperty("reason")}");
                        }
                    }
                    File.Delete(input);
                }
                wav.Position = 0;
                using var header = new BinaryWriter(wav, Encoding.ASCII, true);
                header.Write("RIFF"u8); header.Write((uint)(pcmBytes + 36)); header.Write("WAVEfmt "u8);
                header.Write(16); header.Write((short)1); header.Write((short)1); header.Write(LocalSpeech.SampleRate);
                header.Write(LocalSpeech.BytesPerSecond); header.Write((short)2); header.Write((short)16);
                header.Write("data"u8); header.Write((uint)pcmBytes); header.Flush(); wav.Flush(true);
            }
            // Retain recoverable packets so the existing recovery and privacy paths work unchanged.
            await using (var input = File.OpenRead(audio))
            {
                var buffer = new byte[LocalStore.MaxPacketBytes]; int count, sequence = 0; long consumed = 0;
                while ((count = await input.ReadAsync(buffer, cancel)) > 0) {
                    output.Append(merged, new MediaPacket(sequence++, Math.Max(0, consumed - 44) * 1000.0 / LocalSpeech.BytesPerSecond, buffer[..count], "merged-audio")); consumed += count;
                }
            }
            await File.WriteAllTextAsync(Path.Combine(destination, "merge.json"), JsonSerializer.Serialize(new { parts, sampleRate = LocalSpeech.SampleRate, channels = 1, timestampBasis = "decoded audio; pause-adjusted slides are approximate" }, LocalStore.Json), cancel);
            output.Rename(merged, title);
            output.Finish(merged, "merged-recordings");
            output.Recover(merged);
            cancel.ThrowIfCancellationRequested();
            Directory.Move(destination, Path.Combine(source.Root, merged));
            return merged;
        }
        finally
        {
            foreach (var held in locks) held.Dispose();
            // Only this operation's private, randomly named staging directory is removed.
            Directory.Delete(staging, true);
        }
    }

    private static void Safe(string path) {
        if ((File.Exists(path) || Directory.Exists(path)) && (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0) throw new InvalidDataException("Unsafe recording path.");
    }
    private static async Task Reconstruct(string folder, string target, int expected, CancellationToken cancel)
    {
        await using var output = new FileStream(target, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, true);
        var files = Directory.GetFiles(folder, "*.chunk").Order(StringComparer.Ordinal).ToArray();
        if (files.Length != expected) throw new InvalidDataException("Recording changed during merge.");
        double last = 0;
        for (int i = 0; i < files.Length; i++) {
            Safe(files[i]); var packet = LocalStore.ReadPacket(files[i]);
            if (packet.Sequence != i || Path.GetFileName(files[i]) != $"{i:D8}.chunk" || !double.IsFinite(packet.AtMs) || packet.AtMs < last) throw new InvalidDataException("Recording packet gap.");
            await output.WriteAsync(packet.Bytes, cancel); last = packet.AtMs;
        }
    }
    internal static double AudioTime(string folder, double atMs)
    {
        var file = Path.Combine(folder, "events.ndjson");
        if (!File.Exists(file)) return atMs;
        Safe(file); double start = 0, paused = 0; double? pause = null;
        foreach (var line in File.ReadLines(file)) {
            using var doc = JsonDocument.Parse(line); var e = doc.RootElement;
            var time = e.GetProperty("atMs").GetDouble(); if (!double.IsFinite(time) || time < 0) throw new InvalidDataException("Invalid event clock.");
            if (time > atMs) break;
            switch (e.GetProperty("type").GetString()) {
                case "started": start = time; break;
                case "paused": pause ??= time; break;
                case "resumed": if (pause is double p) paused += time - p; pause = null; break;
            }
        }
        return Math.Max(0, atMs - start - paused - (pause is double pending ? atMs - pending : 0));
    }
}
