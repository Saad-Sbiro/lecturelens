using System.IO;
using LectureLens.Core;
using LectureLens.Storage;
using LectureLens.Desktop;
using LectureLens.Desktop.Adapters;
internal static class LessonChecks
{
 internal static async Task Run(string root)
 {
  var storage=Path.Combine(root,"lessons");
  using var store=new LocalStore(storage);
  var id=store.Create("audio/webm;codecs=opus");
  store.Append(id,new MediaPacket(0,0,[1,2,3,4],"fixture"));
  store.Finish(id,"user-stop");
  var folder=Path.Combine(storage,id);
  store.Rename(id,"Analyse / semaine 1");
  if(store.List().Single().Title!="Analyse / semaine 1")throw new Exception("Rename not persisted");
  Console.WriteLine("PASS rename persists as metadata without moving the lesson");
  try{store.Rename(id,new string('x',121));throw new Exception("Long name accepted");}catch(InvalidDataException){}
  var text=Path.Combine(root,"import.txt");File.WriteAllText(text,string.Join(" ",Enumerable.Repeat("Le cours explique les besoins.",120)));
  var sources=LessonTranscript.Import(text);
  LessonTranscript.Save(folder,sources);
  if(LessonTranscript.Load(folder).Length!=sources.Length)throw new Exception("Transcript reload failed");
  var other=Path.Combine(root,"other");Directory.CreateDirectory(other);
  if(LessonTranscript.Load(other).Length!=0)throw new Exception("Transcript leaked to another lesson");
  File.WriteAllText(Path.Combine(folder,"lesson.json.partial"),"interrupted metadata");
  if(store.List().Single().Problems.Length!=0)throw new Exception("Metadata blocks recovery");
  Console.WriteLine("PASS transcript follows session ID and interrupted metadata does not block capture");
  File.WriteAllText(Path.Combine(folder,"latest-export.json"),"{}");
  File.WriteAllText(Path.Combine(folder,"audio.webm"),"fixture");
  store.RemoveRaw(id);
  var retained=store.List().Single();
  if(retained.State!="archived"||retained.Title!="Analyse / semaine 1"||LessonTranscript.Load(folder).Length!=sources.Length||Directory.GetFiles(folder,"*.chunk").Length!=0||File.Exists(Path.Combine(folder,"audio.webm")))
   throw new Exception("Retention violated");
  Console.WriteLine("PASS raw cleanup preserves library title and transcript");
  var notes=await new ExtractiveNotes(retained.Title).Generate(sources,new("brief","fr",false),CancellationToken.None);
  NotesValidation.Validate(notes,sources);
  if(notes.Sections.Any(s=>s.SourceIds.Any(id=>!sources.Any(x=>x.Id==id))))throw new Exception("Offline references invalid");
  Console.WriteLine("PASS offline notes need no account and cite original excerpts");
 }
}
