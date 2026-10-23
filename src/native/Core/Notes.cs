namespace LectureLens.Core;
public record NoteSource(string Id, string Label, string Text, double? Start = null, double? End = null, string? TimestampBasis = null, int? SlideId = null, double? Confidence = null);
public record NoteSection(string Title, string[] Bullets, string[] SourceIds);
public record StudyNotes(string Title, string Overview, NoteSection[] Sections);
public record NotesPreferences(string Detail, string Language, bool IncludeSlides, string? Topic = null);
public interface ITextGenerator { Task<StudyNotes> Generate(IReadOnlyList<NoteSource> sources, NotesPreferences preferences, CancellationToken cancellation); }
public interface IDocumentRenderer { Task<string> Render(StudyNotes notes, IReadOnlyList<NoteSource> sources, NotesPreferences preferences, string sessionFolder, string outputFolder, CancellationToken cancellation); }
public static class NotesValidation
{
 public static void Validate(StudyNotes notes, IReadOnlyList<NoteSource> sources)
 {
  var ids=sources.Select(s=>s.Id).ToHashSet();
  if(string.IsNullOrWhiteSpace(notes.Title)||notes.Title.Length>300||notes.Overview is null||notes.Overview.Length>100000||notes.Sections is null||notes.Sections.Length is <1 or >1000) throw new InvalidDataException("Invalid notes response");
  foreach(var s in notes.Sections) if(s is null||string.IsNullOrWhiteSpace(s.Title)||s.Title.Length>300||s.Bullets is null||s.Bullets.Length is <1 or >30||s.Bullets.Any(b=>string.IsNullOrWhiteSpace(b)||b.Length>6000)||s.SourceIds is null||s.SourceIds.Length==0||s.SourceIds.Any(id=>!ids.Contains(id))) throw new InvalidDataException("Invalid notes or missing source reference");
 }
}
