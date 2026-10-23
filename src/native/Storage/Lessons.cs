using System.Text.Json;
namespace LectureLens.Storage;
public sealed partial class LocalStore
{
    private string ReadTitle(string id)
    {
        var path = Path.Combine(Folder(id), "lesson.json");
        try { if (File.Exists(path)) return JsonDocument.Parse(File.ReadAllText(path)).RootElement.GetProperty("title").GetString() ?? id[..8]; }
        catch (Exception e) when (e is IOException or JsonException or KeyNotFoundException) { }
        return id[..8];
    }

    public void Rename(string id, string title)
    {
        title = title.Trim();
        if (title.Length is < 1 or > 120 || title.Any(char.IsControl))
            throw new InvalidDataException("Use a lesson name of 1–120 characters.");
        var folder = Folder(id);
        if (!File.Exists(Path.Combine(folder, "manifest.json"))) throw new InvalidDataException("Lesson not found");
        // A name is metadata, never a filesystem path: recordings and references keep their IDs.
        var file = Path.Combine(folder, "lesson.json");
        File.WriteAllText(file + ".partial", JsonSerializer.Serialize(new { title, schemaVersion = 1 }, Json));
        File.Move(file + ".partial", file, true);
    }

    public void RemoveRaw(string id)
    {
        if (IsActive(id)) throw new InvalidOperationException("Cannot remove active media");
        var folder = Folder(id);
        if ((File.GetAttributes(folder) & FileAttributes.ReparsePoint) != 0) throw new InvalidDataException("Unsafe session path");
        if (!File.Exists(Path.Combine(folder, "latest-export.json"))) throw new InvalidDataException("Export the lesson first");
        // Commit the retained lesson state before cleanup. An interrupted cleanup is safe to retry.
        var marker = Path.Combine(folder, "raw-removed.json");
        if (!File.Exists(marker)) WriteJson(marker, new { removedAt = DateTimeOffset.UtcNow });
        foreach (var file in Directory.GetFiles(folder, "*.chunk").Concat(Directory.GetFiles(folder, "*.chunk.partial")))
            File.Delete(file);
        foreach (var name in new[] { "audio.wav", "audio.wav.partial", "audio.webm", "recording.webm", "audio.webm.partial", "recording.webm.partial" })
            File.Delete(Path.Combine(folder, name));
        foreach (var name in new[] { "slides", "speech-work" })
        {
            var path = Path.Combine(folder, name);
            if (!Directory.Exists(path)) continue;
            if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0) throw new InvalidDataException("Unsafe media path");
            Directory.Delete(path, true);
        }
    }
}
