using System.IO;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using LectureLens.Core;
using LectureLens.Desktop.Adapters;
var root=Path.Combine(Path.GetTempPath(),"lecturelens-notes-check-"+Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(root);
var source=new[]{new NoteSource("S1","00:00:00","Le cycle de vie comprend analyse, conception, développement et tests. Les tests vérifient le comportement attendu.")};
var notes=new StudyNotes("Comprendre le cycle de vie logiciel","Un parcours simple, des besoins aux tests.",new[]{new NoteSection("Les étapes essentielles",new[]{"Analyser les besoins avant la conception.","Développer la solution puis vérifier son comportement avec des tests."},new[]{"S1"})});
void Check(bool ok,string label){if(!ok)throw new Exception(label);Console.WriteLine("PASS "+label);}
var handler=new FakeHandler(JsonSerializer.Serialize(notes));
using var http=new HttpClient(handler);
var adapter=new NvidiaNotes("test-model",Path.Combine(root,"cache"),http);
var prefs=new NotesPreferences("standard","fr",false);
var first=await adapter.Generate(source,prefs,CancellationToken.None);
var cached=await adapter.Generate(source,prefs,CancellationToken.None);
Check(first.Title==notes.Title&&cached.Title==notes.Title&&handler.Calls==1,"provider parsing and exact-input cache");
await adapter.Generate(source,prefs with{Detail="brief"},CancellationToken.None);
Check(handler.Calls==2,"detail changes invalidate cache");
try { NotesValidation.Validate(notes with{Sections=new[]{new NoteSection("Bad",new[]{"text"},new[]{"unknown"})}},source);throw new Exception("unknown reference accepted"); }catch(InvalidDataException){Console.WriteLine("PASS rejects unknown source reference");}
var html=BrowserPdf.Compose(notes with{Title="<script>alert(1)</script>"},source,"fr","standard",Array.Empty<(string,string)>());
Check(!html.Contains("<script>")&&html.Contains("&lt;script&gt;"),"model output HTML escaped");
Check(!html.Contains("<img"),"no-slide export contains no images");
var withImage=BrowserPdf.Compose(notes,source,"fr","standard",new[]{("Slide","YWJj")});
Check(withImage.Contains("<img"),"slide appendix selectable");
using var badHttp=new HttpClient(new FakeHandler(JsonSerializer.Serialize(notes),"length"));
try {await new NvidiaNotes("test",Path.Combine(root,"bad"),badHttp).Generate(source,prefs,CancellationToken.None);throw new Exception("truncation accepted");}catch(InvalidDataException){Console.WriteLine("PASS truncated response rejected");}
if(args.Contains("--render")){
 var output=Path.GetFullPath("artifacts/pdf-preview");
 var demoSources=new[]{new NoteSource("S1","first","Analyser les besoins.",0,10,"decoded-audio",0),new NoteSource("S2","second","Concevoir puis tester.",20,30,"decoded-audio",1)};
 var demoNotes=new StudyNotes("Du besoin à la validation","Comprendre le problème, construire une solution, puis vérifier son comportement.",new[]{
  new NoteSection("Comprendre avant de construire",new[]{"L’analyse clarifie le problème à résoudre et le résultat attendu.","Les contraintes servent à évaluer si une solution est adaptée au besoin."},new[]{"S1"}),
  new NoteSection("Concevoir, tester et améliorer",new[]{"La conception organise la solution avant sa réalisation.","Les tests comparent le comportement observé au résultat attendu. Ils aident à identifier les corrections nécessaires."},new[]{"S2"})});
 var pdf=await new BrowserPdf().Render(demoNotes,demoSources,prefs with{IncludeSlides=true},Path.GetFullPath("artifacts/pdf-preview-source"),output,CancellationToken.None);
 Check(File.ReadAllBytes(pdf).Take(5).SequenceEqual("%PDF-"u8.ToArray()),"actual PDF header");
 Console.WriteLine(pdf);
}
await CloudAndTimelineChecks.Run(root);
Directory.Delete(root,true);
sealed class FakeHandler(string content,string finish="stop"):HttpMessageHandler{
 public int Calls;
 protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken token){
  Calls++;token.ThrowIfCancellationRequested();
  return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK){Content=new StringContent(JsonSerializer.Serialize(new{choices=new[]{new{finish_reason=finish,message=new{content}}}}))});
 }
}
