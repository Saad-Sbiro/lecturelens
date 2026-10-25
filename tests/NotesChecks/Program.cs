using System.IO;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using LectureLens.Core;
using LectureLens.Desktop.Adapters;
var root=Path.Combine(Path.GetTempPath(),"lecturelens-notes-check-"+Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(root);
var source=LessonContentChecks.DemoSources;
var notes=LessonContentChecks.DemoNotes;
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
var arabicHandler=new FakeHandler(JsonSerializer.Serialize(LessonContentChecks.ArabicNotes));
using var arabicHttp=new HttpClient(arabicHandler);
var arabicAdapter=new NvidiaNotes("test-model",Path.Combine(root,"arabic-cache"),arabicHttp);
var arabic=await arabicAdapter.Generate(source,prefs with{Language="ar"},CancellationToken.None);
Check(arabic.Title==LessonContentChecks.ArabicNotes.Title&&arabicHandler.LastRequest.Contains("Modern Standard Arabic"),"Arabic output uses registry instructions and parses Unicode with French sources");
Check(!handler.LastRequest.Contains("Modern Standard Arabic"),"Arabic locale instructions do not affect French generation");
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
 var demoSources=LessonContentChecks.DemoSources;
 var demoNotes=LessonContentChecks.DemoNotes;
 var pdf=await new BrowserPdf().Render(demoNotes,demoSources,prefs,root,output,CancellationToken.None);
 await new BrowserPdf().Render(LessonContentChecks.ArabicNotes,demoSources,prefs with{Language="ar"},root,Path.Combine(output,"arabic"),CancellationToken.None);
 Check(File.ReadAllBytes(pdf).Take(5).SequenceEqual("%PDF-"u8.ToArray()),"actual PDF header");
 Console.WriteLine(pdf);
}
await LessonContentChecks.Run(root);
await CloudAndTimelineChecks.Run(root);
await PdfChecks.Run(root,notes,source);
await LessonChecks.Run(root);
Directory.Delete(root,true);
sealed class FakeHandler(string content,string finish="stop"):HttpMessageHandler{
 public int Calls;
 public string LastRequest="";
 protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken token){
  Calls++;token.ThrowIfCancellationRequested();
  LastRequest=await request.Content!.ReadAsStringAsync(token);
  return new HttpResponseMessage(HttpStatusCode.OK){Content=new StringContent(JsonSerializer.Serialize(new{choices=new[]{new{finish_reason=finish,message=new{content}}}}))};
 }
}
