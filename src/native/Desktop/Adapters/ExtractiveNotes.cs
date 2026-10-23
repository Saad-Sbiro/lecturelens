using System.IO;
using System.Text.Json;
using LectureLens.Core;
namespace LectureLens.Desktop.Adapters;

// A no-network fallback: clearly labelled excerpts, never presented as AI synthesis or translation.
internal sealed class ExtractiveNotes(string title) : ITextGenerator
{
    public Task<StudyNotes> Generate(IReadOnlyList<NoteSource> sources, NotesPreferences preferences, CancellationToken cancellation)
    {
        cancellation.ThrowIfCancellationRequested();
        using var registry = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "languages.json")));
        var labels = registry.RootElement.GetProperty(preferences.Language).GetProperty("offlineNotes");
        var take = preferences.Detail switch { "brief" => 1, "detailed" => 6, _ => 3 };
        var sections = new List<NoteSection>();
        // Distribute coverage over the whole lesson; detail controls verbatim excerpt length, not invented material.
        for (int offset = 0; offset < sources.Count; offset += 8)
        {
            var group = sources.Skip(offset).Take(8).ToArray();
            var selected = group.Where((s, i) => i == 0 || i == group.Length - 1 || (preferences.Detail != "brief" && i % 2 == 0)).ToArray();
            var bullets = selected.Select(s => {
                var sentences = System.Text.RegularExpressions.Regex.Split(s.Text, @"(?<=[.!?])\s+");
                return string.Join(" ", sentences.Take(take));
            }).ToArray();
            sections.Add(new(labels.GetProperty("section").GetString()! + " " + (sections.Count + 1), bullets, selected.Select(s => s.Id).ToArray()));
        }
        return Task.FromResult(new StudyNotes(title, labels.GetProperty("notice").GetString()!, sections.ToArray()));
    }
}
