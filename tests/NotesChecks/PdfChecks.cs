using System.IO;
using System.Security.Cryptography;
using System.Text.Json;
using LectureLens.Core;
using LectureLens.Desktop.Adapters;

internal static class PdfChecks
{
 internal static async Task Run(string root,StudyNotes notes,NoteSource[] sources)
 {
  static void Check(bool ok,string label){if(!ok)throw new Exception(label);Console.WriteLine("PASS "+label);}
  var longSources=sources.Select(s=>s with{Text=new string('x',3000)+"PRIVATE_TRANSCRIPT_TAIL"}).ToArray();
  var html=BrowserPdf.Compose(notes,longSources,"fr","brief",Array.Empty<(string,string)>());
  Check(!html.Contains("PRIVATE_TRANSCRIPT_TAIL")&&!html.Contains(new string('x',121)),"brief PDF keeps compact source references");
  var imageHtml=BrowserPdf.Compose(notes,sources,"en","standard",new[]{("<bad>","' onerror='alert(1)")});
  Check(!imageHtml.Contains(" onerror='")&&imageHtml.Contains("&lt;bad&gt;"),"image attributes escaped");
  var folder=Path.Combine(root,"pdf-checks");var slides=Path.Combine(folder,"slides");Directory.CreateDirectory(slides);
  // Minimal envelope fixture tests streaming and integrity without running a browser or capture.
  byte[] jpeg=[255,216,255,217];var encoded=Convert.ToBase64String(jpeg);var sha=Convert.ToHexStringLower(SHA256.HashData(jpeg));
  for(int i=0;i<720;i++)await File.WriteAllTextAsync(Path.Combine(slides,$"{i:D5}.slide.json"),JsonSerializer.Serialize(new{slide=i,atMs=i*5000,data=encoded,sha256=sha}));
  var target=Path.Combine(folder,"notes.html");var prefs=new NotesPreferences("brief","fr",true);
  await BrowserPdf.WriteHtml(notes,sources,prefs,folder,target,CancellationToken.None);
  var streamed=await File.ReadAllTextAsync(target);
  Check(streamed.Split("<figure class='slide'>").Length-1==720,"streams all 720 captured slides without 100-slide limit");
  await BrowserPdf.WriteHtml(notes,sources,prefs with{IncludeSlides=false},folder,target,CancellationToken.None);
  Check(!(await File.ReadAllTextAsync(target)).Contains("<img"),"saved HTML honors no-images preference");
  var before=await File.ReadAllTextAsync(target);using var canceled=new CancellationTokenSource();canceled.Cancel();
  try{await BrowserPdf.WriteHtml(notes,sources,prefs,folder,target,canceled.Token);throw new Exception("cancel ignored");}catch(OperationCanceledException){}
  Check(await File.ReadAllTextAsync(target)==before,"cancel preserves previous successful HTML");
  var bad=Path.Combine(slides,"00000.slide.json");
  await File.WriteAllTextAsync(bad,JsonSerializer.Serialize(new{slide=0,atMs=0,data=encoded,sha256="tampered"}));
  try{await BrowserPdf.WriteHtml(notes,sources,prefs,folder,target,CancellationToken.None);throw new Exception("corruption accepted");}catch(InvalidDataException){}
  Check(await File.ReadAllTextAsync(target)==before,"corrupt slide rejected without replacing successful HTML");
  await File.WriteAllTextAsync(bad,new string(' ',700001));
  try{await BrowserPdf.ReadSlide(bad,CancellationToken.None);throw new Exception("oversize accepted");}catch(InvalidDataException){Console.WriteLine("PASS oversized slide rejected before JSON parsing");}
  var falsePdf=Path.Combine(folder,"bad.pdf");await File.WriteAllTextAsync(falsePdf,new string('x',200));
  Check(!BrowserPdf.IsPdf(falsePdf),"non-PDF renderer output rejected");
 }
}
