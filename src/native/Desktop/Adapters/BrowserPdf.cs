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
 static string ReferenceLabel(NoteSource source)=>source.Label==source.Id||source.Label.StartsWith(source.Id+" · ",StringComparison.Ordinal)?source.Label:source.Id+" · "+source.Label;
 internal static string Compose(StudyNotes notes,IReadOnlyList<NoteSource> sources,string language,string detail,IReadOnlyList<(string Label,string Image)> images)
 {
  var html=new StringBuilder(Body(notes,sources,language,detail));
  foreach(var slide in images)html.Append(SlideHtml(slide.Label,slide.Image,language));
  return html.Append(SourceIndex(notes,sources,language)).Append("</body></html>").ToString();
 }
 static string Body(StudyNotes notes,IReadOnlyList<NoteSource> sources,string language,string detail)
 {
  NotesValidation.Validate(notes,sources);
  using var registry=JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"languages.json")));
  var locale=registry.RootElement.GetProperty(language);
  var direction=locale.GetProperty("direction").GetString()=="rtl"?"rtl":"ltr";
  var html=new StringBuilder("<!doctype html><html lang='"+H(language)+"' dir='"+direction+"'><meta charset='utf-8'><meta http-equiv='Content-Security-Policy' content=\"default-src 'none'; img-src data:; style-src 'unsafe-inline'\"><title>LectureLens</title><style>@page{size:A4;margin:19mm 17mm 20mm;@bottom-left{content:'LectureLens';font:9pt Arial;color:#80745e}@bottom-right{content:counter(page);font:9pt Arial;color:#80745e}}*{box-sizing:border-box}body{font:11pt/1.65 'Segoe UI',Arial,sans-serif;color:#28231b;margin:0;overflow-wrap:anywhere}h1{font-size:34pt;line-height:1.13;letter-spacing:-1.2px;margin:25mm 0 10mm}h2{font-size:19pt;line-height:1.3;break-after:avoid;color:#33250c}h3{font-size:13pt;break-after:avoid}p,li{orphans:3;widows:3}li{margin:0 0 7pt;padding-left:4pt}section{margin-bottom:10mm}.cover{break-after:page;min-height:230mm;border-top:7px solid #ffb51b;padding:10mm 0}.brand{font-size:13pt;font-weight:700;letter-spacing:2px}.tag{display:inline-block;background:#fff0c4;padding:3mm 5mm;font-size:9pt}.overview{font-size:14pt;color:#625842;white-space:pre-wrap}.rule{border:0;border-top:1px solid #e4dccc;margin:10mm 0}.refs{font-size:9pt;color:#786b54;background:#f6f2e9;padding:8pt 10pt}.sources{border-top:1px solid #e4dccc;padding-top:6mm;margin-top:12mm}.sources p{font-size:9pt;break-inside:avoid}.slide{break-before:page;break-inside:avoid;margin:0}.slide img{max-width:100%;max-height:195mm;object-fit:contain}.warning{font-size:9pt;color:#786b54}nav p{margin:5pt 0}a{color:#6a4c0b;text-decoration:none}</style><body>");
  html.Append("<div class='cover'><div class='brand'>LECTURE<span style='color:#d98500'>LENS</span></div><h1>"+H(notes.Title)+"</h1><span class='tag'>"+H(language)+" · "+H(detail)+"</span><p class='overview'>"+H(notes.Overview)+"</p><hr class='rule'><p class='warning'>"+H(Label(language,"review"))+"</p><nav>");
  for(int i=0;i<notes.Sections.Length;i++)html.Append("<p><a href='#s"+i+"'>"+(i+1).ToString("D2")+" / "+H(notes.Sections[i].Title)+"</a></p>");
  html.Append("</nav></div>");
  for(int i=0;i<notes.Sections.Length;i++){var s=notes.Sections[i];html.Append("<section id='s"+i+"'><h2>"+(i+1).ToString("D2")+" / "+H(s.Title)+"</h2><ul>");foreach(var b in s.Bullets)html.Append("<li>"+H(b)+"</li>");html.Append("</ul><p class='refs'>"+H(string.Join(" · ",s.SourceIds.Select(id=>ReferenceLabel(sources.First(x=>x.Id==id)))))+"</p></section>");}
  return html.ToString();
 }
 static string SlideHtml(string label,string image,string language)=>"<figure class='slide'><h2>"+H(label)+"</h2><p class='warning'>"+H(Label(language,"images"))+"</p><img alt='"+H(label)+"' src='data:image/jpeg;base64,"+H(image)+"'></figure>";
 static string SourceIndex(StudyNotes notes,IReadOnlyList<NoteSource> sources,string language)
 {
  var cited=notes.Sections.SelectMany(section=>section.SourceIds).ToHashSet();
  var html=new StringBuilder("<section class='sources'><h2>"+H(Label(language,"sources"))+"</h2>");
  // Keep references checkable without turning a short summary into a full transcript export.
  foreach(var source in sources.Where(s=>cited.Contains(s.Id))){var limit=Math.Min(120,source.Text.Length);if(limit>0&&char.IsHighSurrogate(source.Text[limit-1]))limit--;var excerpt=source.Text.Length<=limit?source.Text:source.Text[..limit].TrimEnd()+"…";html.Append("<p><b>"+H(ReferenceLabel(source))+"</b><br>"+H(excerpt)+"</p>");}
  return html.Append("</section>").ToString();
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
  var files=preferences.IncludeSlides&&Directory.Exists(slideFolder)?Directory.GetFiles(slideFolder,"*.slide.json").Order(StringComparer.Ordinal).ToArray():[];
  if(files.Length>720)throw new InvalidDataException("The recording exceeds the 720-slide capture limit. Originals were preserved.");
  var partial=html+"."+Guid.NewGuid().ToString("N")+".partial";
  try{
   await using(var writer=new StreamWriter(partial,false,new UTF8Encoding(false))){
    await writer.WriteAsync(Body(notes,sources,preferences.Language,preferences.Detail).AsMemory(),cancellation);
    var seen=new HashSet<int>();
    // A long lecture must not hold hundreds of decoded images and their base64 copies in memory.
    foreach(var file in files){var slide=await ReadSlide(file,cancellation);if(!seen.Add(slide.Number))throw new InvalidDataException("Duplicate slide identifier");await writer.WriteAsync(SlideHtml(slide.Label,slide.Image,preferences.Language).AsMemory(),cancellation);}
    await writer.WriteAsync((SourceIndex(notes,sources,preferences.Language)+"</body></html>").AsMemory(),cancellation);
   }
   cancellation.ThrowIfCancellationRequested();File.Move(partial,html,true);
  }catch{if(File.Exists(partial))File.Delete(partial);throw;}
 }
 internal static async Task<(int Number,string Label,string Image)> ReadSlide(string file,CancellationToken cancellation)
 {
  var info=new FileInfo(file);if(info.Length is <10 or >700000)throw new InvalidDataException("Slide envelope exceeds the storage limit");
  using var document=JsonDocument.Parse(await File.ReadAllTextAsync(file,cancellation));var slide=document.RootElement;
  var number=slide.GetProperty("slide").GetInt32();var atMs=slide.GetProperty("atMs").GetDouble();
  if(number is <0 or >=720||!double.IsFinite(atMs)||atMs<0||atMs>TimeSpan.MaxValue.TotalMilliseconds)throw new InvalidDataException("Invalid slide metadata");
  var bytes=Convert.FromBase64String(slide.GetProperty("data").GetString()!);
  if(bytes.Length is <4 or >500000||bytes[0]!=255||bytes[1]!=216||bytes[^2]!=255||bytes[^1]!=217||Convert.ToHexStringLower(SHA256.HashData(bytes))!=slide.GetProperty("sha256").GetString())throw new InvalidDataException("Slide integrity failure");
  return (number,(number+1)+" · "+TimeSpan.FromMilliseconds(atMs).ToString(@"hh\:mm\:ss"),Convert.ToBase64String(bytes));
 }
}
