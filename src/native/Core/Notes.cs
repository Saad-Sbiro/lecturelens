namespace LectureLens.Core;
public record NoteSource(string Id, string Label, string Text, double? Start = null, double? End = null, string? TimestampBasis = null, int? SlideId = null, double? Confidence = null);
public record NoteDiagram(string Kind, string Title, string[] Labels, string[] SourceIds);
public record SourceDecision(string SourceId, string Action, string Reason);
public record NoteSection(string Title, string[] Bullets, string[] SourceIds, string[]? Paragraphs = null, NoteDiagram[]? Diagrams = null);
public record StudyNotes(string Title, string Overview, NoteSection[] Sections, SourceDecision[]? Decisions = null);
public record NotesPreferences(string Detail, string Language, bool IncludeSlides, string? Topic = null);
public interface ITextGenerator { Task<StudyNotes> Generate(IReadOnlyList<NoteSource> sources, NotesPreferences preferences, CancellationToken cancellation); }
public interface IDocumentRenderer { Task<string> Render(StudyNotes notes, IReadOnlyList<NoteSource> sources, NotesPreferences preferences, string sessionFolder, string outputFolder, CancellationToken cancellation); }
public static class NotesValidation
{
 public static void Validate(StudyNotes notes, IReadOnlyList<NoteSource> sources)
 {
  var ids=sources.Select(s=>s.Id).ToHashSet();
  if(string.IsNullOrWhiteSpace(notes.Title)||notes.Title.Length>300||notes.Overview is null||notes.Overview.Length>100000||notes.Sections is null||notes.Sections.Length>1000) throw new InvalidDataException("Invalid notes response");
  foreach(var s in notes.Sections) {
   if(s is null||string.IsNullOrWhiteSpace(s.Title)||s.Title.Length>300||s.Bullets is null||s.Bullets.Length>30||s.Bullets.Any(b=>!Text(b,6000))||s.SourceIds is null||s.SourceIds.Length==0||s.SourceIds.Any(id=>!ids.Contains(id))||s.Paragraphs is {Length:>10}||s.Paragraphs?.Any(p=>!Text(p,6000))==true||s.Bullets.Length+(s.Paragraphs?.Length??0)==0) throw new InvalidDataException("Invalid notes or missing source reference");
   if(s.Diagrams is {Length:>2})throw new InvalidDataException("Too many diagrams");
   foreach(var d in s.Diagrams??[])if(d is null||d.Kind is not ("sequence" or "concepts" or "comparison")||!Text(d.Title,180)||d.Labels is null||d.Labels.Length is <2 or >6||d.Labels.Any(l=>!Text(l,180))||d.SourceIds is null||d.SourceIds.Length==0||d.SourceIds.Any(id=>!s.SourceIds.Contains(id)))throw new InvalidDataException("Invalid or unreferenced diagram");
  }
  if(notes.Decisions is not null) {
   if(notes.Decisions.Length!=ids.Count||notes.Decisions.Any(d=>d is null||!ids.Contains(d.SourceId)||d.Action is not ("keep" or "mixed" or "exclude")||!Text(d.Reason,1000))||notes.Decisions.Select(d=>d.SourceId).Distinct().Count()!=ids.Count)throw new InvalidDataException("Missing source decisions");
   var excluded=notes.Decisions.Where(d=>d.Action=="exclude").Select(d=>d.SourceId).ToHashSet();
   if(notes.Sections.Any(s=>s.SourceIds.Any(excluded.Contains)))throw new InvalidDataException("Excluded conversation cited as lesson content");
   if(notes.Sections.Length==0&&(notes.Overview.Length!=0||notes.Decisions.Any(d=>d.Action!="exclude")))throw new InvalidDataException("Empty lesson must exclude all sources");
  } else if(notes.Sections.Length==0)throw new InvalidDataException("Empty notes need source decisions");
 }
 public static void ValidateLesson(StudyNotes notes,IReadOnlyList<NoteSource> sources) {
  Validate(notes,sources);
  if(notes.Decisions is null||notes.Sections.Any(s=>s.Paragraphs is not {Length:>0}))throw new InvalidDataException("Lesson notes require explanations and source decisions");
  if(notes.Sections.Any(s=>s.Bullets.Length>3))throw new InvalidDataException("Lesson explanations cannot be replaced by bullet lists");
  var cited=notes.Sections.SelectMany(s=>s.SourceIds).ToHashSet();
  if(notes.Decisions.Any(d=>d.Action!="exclude"&&!cited.Contains(d.SourceId)))throw new InvalidDataException("Retained lesson source has no explanation");
 }
 static bool Text(string? value,int max)=>!string.IsNullOrWhiteSpace(value)&&value.Length<=max;
}
