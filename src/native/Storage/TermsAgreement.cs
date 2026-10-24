using System.Text.Json;

namespace LectureLens.Storage;

public sealed class TermsAgreement(string root)
{
    public sealed record Section(string Title, string Body);
    public sealed record Terms(string Version, string Title, string Intro, string Agreement, string Accept, string Decline, string Close, string SaveError, Section[] Sections);
    public sealed record Acceptance(string Version, DateTimeOffset AcceptedAt);
    public static Terms Document { get; } = LoadDocument();
    string FileName => Path.Combine(root, "terms-acceptance.json");

    static Terms LoadDocument()
    {
        using var stream = typeof(TermsAgreement).Assembly.GetManifestResourceStream("LectureLens.Terms.json")!;
        return JsonSerializer.Deserialize<Terms>(stream, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;
    }

    public bool IsAccepted()
    {
        try
        {
            var saved = JsonSerializer.Deserialize<Acceptance>(File.ReadAllText(FileName));
            return saved?.Version == Document.Version && saved.AcceptedAt != default;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException) { return false; }
    }

    public void Accept(string version)
    {
        if (version != Document.Version) throw new InvalidDataException("Read and accept the current terms first.");
        Directory.CreateDirectory(root);
        // Unique temporary files keep simultaneous desktop/extension acceptance atomic.
        var temporary = FileName + "." + Guid.NewGuid().ToString("N") + ".partial";
        try
        {
            using (var file = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                JsonSerializer.Serialize(file, new Acceptance(version, DateTimeOffset.UtcNow));
                file.Flush(true);
            }
            File.Move(temporary, FileName, true);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
}
