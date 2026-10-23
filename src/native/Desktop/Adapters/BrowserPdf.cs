using System.Diagnostics;
using System.IO;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using LectureLens.Core;
namespace LectureLens.Desktop.Adapters;
internal sealed class BrowserPdf : IDocumentRenderer
{
 static string H(string? s)=>WebUtility.HtmlEncode(s??"");
 internal static string Compose(StudyNotes notes,IReadOnlyList<NoteSource> sources,string language,string detail,IReadOnlyList<(string Label,string Image)> images)
 {
  var html=new StringBuilder(Body(notes,sources,language));
  foreach(var slide in images)html.Append(SlideHtml(slide.Label,slide.Image));
  return html.Append("</body></html>").ToString();
 }
 static string Body(StudyNotes notes,IReadOnlyList<NoteSource> sources,string language,bool sections=true)
 {
  NotesValidation.Validate(notes,sources);
  using var registry=JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"languages.json")));
  var locale=registry.RootElement.GetProperty(language);
  var direction=locale.GetProperty("direction").GetString()=="rtl"?"rtl":"ltr";
  var html=new StringBuilder("<!doctype html><html lang='"+H(language)+"' dir='"+direction+"'><meta charset='utf-8'><meta http-equiv='Content-Security-Policy' content=\"default-src 'none'; img-src data:; style-src 'unsafe-inline'\"><title>LectureLens</title><style>@page{size:A4;margin:19mm 17mm 20mm;@bottom-left{content:'LectureLens';font:9pt Arial;color:#80745e}@bottom-right{content:counter(page);font:9pt Arial;color:#80745e}}*{box-sizing:border-box}body{font:11pt/1.65 'Segoe UI',Arial,sans-serif;color:#28231b;margin:0;overflow-wrap:anywhere}h1{font-size:28pt;line-height:1.13;letter-spacing:-1.2px;margin:8mm 0 5mm}h2{font-size:19pt;line-height:1.3;break-after:avoid;color:#33250c}p,li{orphans:3;widows:3}li{margin:0 0 7pt;padding-left:4pt}section{margin-bottom:10mm}.cover{border-top:7px solid #ffb51b;padding:5mm 0}.brand{font-size:13pt;font-weight:700;letter-spacing:2px}.overview{font-size:12pt;color:#625842;white-space:pre-wrap}.slide{break-inside:avoid;margin:0 0 6mm}.lesson-slide{break-before:page}.cover+.lesson-slide{break-before:auto}.lesson-slide h2{margin-top:0}.slide img{max-width:100%;max-height:110mm;object-fit:contain}.warning{font-size:9pt;color:#786b54}</style><body>");
  html.Append("<header class='cover'><div class='brand'>LECTURE<span style='color:#d98500'>LENS</span></div><h1>"+H(notes.Title)+"</h1>");
  if(!string.IsNullOrWhiteSpace(notes.Overview))html.Append("<p class='overview'>"+H(notes.Overview)+"</p>");
  html.Append("<p class='warning'>"+H(Label(language,"review"))+"</p></header>");
  if(sections)foreach(var section in notes.Sections)html.Append(SectionHtml(section));
  return html.ToString();
 }
 static string SectionHtml(NoteSection section)=>"<section><h2>"+H(section.Title)+"</h2><ul>"+string.Concat(section.Bullets.Select(b=>"<li>"+H(b)+"</li>"))+"</ul></section>";
 static string SlideHtml(string label,string image)=>"<figure class='slide'><img alt='"+H(label)+"' src='data:image/jpeg;base64,"+H(image)+"'></figure>";
 internal static int? SectionSlide(NoteSection section,IReadOnlyList<NoteSource> sources)
 {
  var ids=section.SourceIds.ToHashSet();var linked=sources.Where(s=>ids.Contains(s.Id)).Select(s=>s.SlideId).Distinct().ToArray();
  return linked.Length==1?linked[0]:null;
 }
 static string Label(string language,string key)
 {
  using var registry=JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"languages.json")));
  return registry.RootElement.GetProperty(language).GetProperty("pdfLabels").GetProperty(key).GetString()!;
 }
 public async Task<string> Render(StudyNotes notes,IReadOnlyList<NoteSource> sources,NotesPreferences preferences,string sessionFolder,string outputFolder,CancellationToken cancellation)
 {
  cancellation.ThrowIfCancellationRequested();
  NotesValidation.Validate(notes,sources);
  Directory.CreateDirectory(outputFolder);var html=Path.GetFullPath(Path.Combine(outputFolder,"notes.html"));var pdf=Path.GetFullPath(Path.Combine(outputFolder,"notes.pdf"));
  await WriteHtml(notes,sources,preferences,sessionFolder,html,cancellation);
  var browsers=new[]{Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),"Microsoft","Edge","Application","msedge.exe"),Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),"Microsoft","Edge","Application","msedge.exe"),Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),"Google","Chrome","Application","chrome.exe"),Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),"Google","Chrome","Application","chrome.exe"),Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"Google","Chrome","Application","chrome.exe")};
  var browser=browsers.FirstOrDefault(File.Exists)??throw new FileNotFoundException("PDF export needs installed Microsoft Edge or Chrome. HTML was saved.");
  var attempt=Guid.NewGuid().ToString("N");var temporaryPdf=Path.Combine(Path.GetDirectoryName(pdf)!,"notes-"+attempt+".partial.pdf");
  var psi=new ProcessStartInfo(browser){UseShellExecute=false,CreateNoWindow=true};
  foreach(var arg in new[]{"--headless=new","--disable-gpu","--no-first-run","--no-default-browser-check","--disable-background-networking","--no-pdf-header-footer","--user-data-dir="+Path.GetFullPath(Path.Combine(outputFolder,"browser-profile-"+attempt)),"--print-to-pdf="+temporaryPdf,new Uri(html).AbsoluteUri})psi.ArgumentList.Add(arg);
  cancellation.ThrowIfCancellationRequested();
  using var process=Process.Start(psi)??throw new IOException("PDF renderer could not start");
  using var deadline=CancellationTokenSource.CreateLinkedTokenSource(cancellation);deadline.CancelAfter(TimeSpan.FromMinutes(3));
  try{await process.WaitForExitAsync(deadline.Token);}
  catch(OperationCanceledException){if(!process.HasExited)process.Kill(true);if(!cancellation.IsCancellationRequested)throw new TimeoutException("PDF export took too long. Your notes and HTML remain saved; retry with fewer images.");throw;}
  cancellation.ThrowIfCancellationRequested();
  if(process.ExitCode!=0||!IsPdf(temporaryPdf))throw new IOException("PDF export failed; HTML is available in the output folder");
  File.Move(temporaryPdf,pdf,true);
  return pdf;
 }
 internal static bool IsPdf(string path)
 {
  if(!File.Exists(path)||new FileInfo(path).Length<100)return false;
  using var stream=File.OpenRead(path);Span<byte> header=stackalloc byte[5];
  return stream.Read(header)==5&&header.SequenceEqual("%PDF-"u8);
 }
 internal static async Task WriteHtml(StudyNotes notes,IReadOnlyList<NoteSource> sources,NotesPreferences preferences,string sessionFolder,string html,CancellationToken cancellation)
 {
  cancellation.ThrowIfCancellationRequested();
  var slideFolder=Path.Combine(sessionFolder,"slides");
  var excluded=SlideSelection.Load(sessionFolder);
  var files=preferences.IncludeSlides&&Directory.Exists(slideFolder)?Directory.GetFiles(slideFolder,"*.slide.json").Order(StringComparer.Ordinal).ToArray():[];
  if(files.Length>720)throw new InvalidDataException("The recording exceeds the 720-slide capture limit. Originals were preserved.");
  files=files.Where(path=>!excluded.Contains(SlideNumber(path))).ToArray();
  var partial=html+"."+Guid.NewGuid().ToString("N")+".partial";
  try{
   await using(var writer=new StreamWriter(partial,false,new UTF8Encoding(false))){
    await writer.WriteAsync(Body(notes,sources,preferences.Language,false).AsMemory(),cancellation);
    var available=new HashSet<int>();
    foreach(var file in files){var slide=await ReadSlide(file,cancellation);if(!available.Add(slide.Number))throw new InvalidDataException("Duplicate slide identifier");}
    foreach(var section in notes.Sections.Where(s=>SectionSlide(s,sources) is not int id||!available.Contains(id)))
     await writer.WriteAsync(SectionHtml(section).AsMemory(),cancellation);
    // Stream one image at a time. No appendix of unexplained images and no repeated opening summary.
    foreach(var file in files){
     var slide=await ReadSlide(file,cancellation);
     var linked=notes.Sections.Where(s=>SectionSlide(s,sources)==slide.Number).ToArray();
     await writer.WriteAsync(("<article class='lesson-slide'>"+SlideHtml(slide.Label,slide.Image)).AsMemory(),cancellation);
     if(linked.Length==0)await writer.WriteAsync(("<p class='warning'>"+H(Label(preferences.Language,"unmatched"))+"</p>").AsMemory(),cancellation);
     else {
      await writer.WriteAsync(("<p class='warning'>"+H(Label(preferences.Language,"aligned"))+"</p>").AsMemory(),cancellation);
      foreach(var section in linked)await writer.WriteAsync(SectionHtml(section).AsMemory(),cancellation);
     }
     await writer.WriteAsync("</article>".AsMemory(),cancellation);
    }
    await writer.WriteAsync("</body></html>".AsMemory(),cancellation);
   }
   cancellation.ThrowIfCancellationRequested();File.Move(partial,html,true);
  }catch{if(File.Exists(partial))File.Delete(partial);throw;}
 }
 static int SlideNumber(string file)
 {
  if(new FileInfo(file).Length is <10 or >700000)throw new InvalidDataException("Slide envelope exceeds the storage limit");
  using var document=JsonDocument.Parse(File.ReadAllText(file));
  var number=document.RootElement.GetProperty("slide").GetInt32();
  if(number is <0 or >=720)throw new InvalidDataException("Invalid slide metadata");
  return number;
 }
 internal static async Task<(int Number,string Label,string Image,double AtMs)> ReadSlide(string file,CancellationToken cancellation)
 {
  var info=new FileInfo(file);if(info.Length is <10 or >700000)throw new InvalidDataException("Slide envelope exceeds the storage limit");
  using var document=JsonDocument.Parse(await File.ReadAllTextAsync(file,cancellation));var slide=document.RootElement;
  var number=slide.GetProperty("slide").GetInt32();var atMs=slide.GetProperty("atMs").GetDouble();
  if(number is <0 or >=720||!double.IsFinite(atMs)||atMs<0||atMs>TimeSpan.MaxValue.TotalMilliseconds)throw new InvalidDataException("Invalid slide metadata");
  var bytes=Convert.FromBase64String(slide.GetProperty("data").GetString()!);
  if(bytes.Length is <4 or >500000||bytes[0]!=255||bytes[1]!=216||bytes[^2]!=255||bytes[^1]!=217||Convert.ToHexStringLower(SHA256.HashData(bytes))!=slide.GetProperty("sha256").GetString())throw new InvalidDataException("Slide integrity failure");
  return (number,(number+1)+" · "+TimeSpan.FromMilliseconds(atMs).ToString(@"hh\:mm\:ss"),Convert.ToBase64String(bytes),atMs);
 }
}
