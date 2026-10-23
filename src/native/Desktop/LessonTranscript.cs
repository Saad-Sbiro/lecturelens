using System.IO;
using System.Text.Json;
using LectureLens.Core;
namespace LectureLens.Desktop;

// Lesson-owned text is loaded by stable session ID, never by matching a filename or lesson title.
internal static class LessonTranscript
{
    internal static readonly JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive = true, WriteIndented = true };
    internal static string PathFor(string folder) => Path.Combine(folder, "notes-transcript.json");
    internal static NoteSource[] Load(string folder)
    {
        var path = PathFor(folder);
        if (!File.Exists(path)) return [];
        if (new FileInfo(path).Length > 4_000_000) throw new InvalidDataException("Transcript exceeds the preview size limit.");
        var sources = JsonSerializer.Deserialize<NoteSource[]>(File.ReadAllText(path), Json) ?? [];
        if (sources.Any(s => s is null || string.IsNullOrWhiteSpace(s.Id) || string.IsNullOrWhiteSpace(s.Text)) ||
            sources.Select(s => s.Id).Distinct().Count() != sources.Length)
            throw new InvalidDataException("Invalid saved transcript.");
        return sources;
    }
    internal static void Save(string folder, NoteSource[] sources)
    {
        var path = PathFor(folder);
        File.WriteAllText(path + ".partial", JsonSerializer.Serialize(sources, Json));
        File.Move(path + ".partial", path, true);
    }
    internal static NoteSource[] Import(string path)
    {
        if (new FileInfo(path).Length > 4_000_000) throw new InvalidDataException("Transcript limit: 4 MB");
        var raw = File.ReadAllText(path);
        var texts = new List<(string Label, string Text, double? Start, double? End, string? Basis, double? Confidence)>();
        if (Path.GetExtension(path).Equals(".json", StringComparison.OrdinalIgnoreCase))
        {
            using var doc = JsonDocument.Parse(raw);
            var basis = doc.RootElement.TryGetProperty("timestampBasis", out var clock) && clock.GetString()?.StartsWith("decoded-audio", StringComparison.Ordinal) == true ? "decoded-audio" : null;
            foreach (var s in doc.RootElement.GetProperty("segments").EnumerateArray())
            {
                if (s.TryGetProperty("kept", out var kept) && kept.ValueKind == JsonValueKind.False) continue;
                var label = s.TryGetProperty("start", out var start) ? TimeSpan.FromSeconds(start.GetDouble()).ToString(@"hh\:mm\:ss") : "";
                double? from = s.TryGetProperty("start", out var a) ? a.GetDouble() : null;
                double? to = s.TryGetProperty("end", out var b) ? b.GetDouble() : null;
                if (from is double f && (!double.IsFinite(f) || f < 0) || to is double t && (!double.IsFinite(t) || t < (from ?? 0))) throw new InvalidDataException("Invalid transcript timing.");
                double? confidence = s.TryGetProperty("confidence", out var certainty) && certainty.ValueKind == JsonValueKind.Number ? certainty.GetDouble() : null;
                if (confidence is double c && (!double.IsFinite(c) || c is < 0 or > 1)) throw new InvalidDataException("Invalid transcript confidence.");
                texts.Add((label, s.GetProperty("text").GetString() ?? "", from, to, basis, confidence));
            }
        }
        else texts.Add(("", raw, null, null, null, null));
        var output = new List<NoteSource>();
        foreach (var (label, text, from, to, basis, confidence) in texts)
        {
            for (int offset = 0; offset < text.Length;)
            {
                var length = Math.Min(2000, text.Length - offset);
                if (offset + length < text.Length)
                {
                    var boundary = text.LastIndexOf(' ', offset + length - 1, length);
                    if (boundary > offset + length / 2) length = boundary - offset + 1;
                }
                var part = text.Substring(offset, length).Trim(); offset += length;
                if (part.Length == 0) continue;
                var id = "S" + (output.Count + 1);
                output.Add(new(id, label.Length > 0 ? id + " · " + label : id, part, from, to, basis, null, confidence));
            }
        }
        if (output.Count == 0 || output.Sum(s => s.Text.Length) > 500000)
            throw new InvalidDataException("Use a non-empty transcript up to 500,000 characters; nothing was truncated.");
        return output.ToArray();
    }
}

