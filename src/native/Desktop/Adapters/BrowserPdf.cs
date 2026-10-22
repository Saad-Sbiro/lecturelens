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
  var html=new StringBuilder("<!doctype html><html lang='"+H(language)+"'><meta charset='utf-8'><meta http-equiv='Content-Security-Policy' content=\"default-src 'none'; img-src data:; style-src 'unsafe-inline'\"><title>LectureLens</title><style>@page{size:A4;margin:19mm 17mm 20mm;@bottom-left{content:'LectureLens';font:9pt Arial;color:#80745e}@bottom-right{content:counter(page);font:9pt Arial;color:#80745e}}*{box-sizing:border-box}body{font:11pt/1.65 'Segoe UI',Arial,sans-serif;color:#28231b;margin:0}h1{font-size:34pt;line-height:1.13;letter-spacing:-1.2px;margin:25mm 0 10mm}h2{font-size:19pt;line-height:1.3;break-after:avoid;color:#33250c}h3{font-size:13pt;break-after:avoid}p,li{orphans:3;widows:3}li{margin:0 0 7pt;padding-left:4pt}section{margin-bottom:10mm}.cover{break-after:page;min-height:230mm;border-top:7px solid #ffb51b;padding:10mm 0}.brand{font-size:13pt;font-weight:700;letter-spacing:2px}.tag{display:inline-block;background:#fff0c4;padding:3mm 5mm;font-size:9pt}.overview{font-size:14pt;color:#625842;white-space:pre-wrap}.rule{border:0;border-top:1px solid #e4dccc;margin:10mm 0}.refs{font-size:9pt;color:#786b54;background:#f6f2e9;padding:8pt 10pt}.sources{break-before:page}.sources p{font-size:9pt;white-space:pre-wrap}.slide{break-before:page;break-inside:avoid}.slide img{max-width:100%;max-height:215mm;object-fit:contain}.warning{font-size:9pt;color:#786b54}nav p{margin:5pt 0}a{color:#6a4c0b;text-decoration:none}</style>");
  html.Append("<div class='cover'><div class='brand'>LECTURE<span style='color:#d98500'>LENS</span></div><h1>"+H(notes.Title)+"</h1><span class='tag'>"+H(language)+" · "+H(detail)+"</span><p class='overview'>"+H(notes.Overview)+"</p><hr class='rule'><p class='warning'>"+H(Label(language,"review"))+"</p><nav>");
  for(int i=0;i<notes.Sections.Length;i++)html.Append("<p><a href='#s"+i+"'>"+(i+1).ToString("D2")+" / "+H(notes.Sections[i].Title)+"</a></p>");
  html.Append("</nav></div>");
  for(int i=0;i<notes.Sections.Length;i++){var s=notes.Sections[i];html.Append("<section id='s"+i+"'><h2>"+(i+1).ToString("D2")+" / "+H(s.Title)+"</h2><ul>");foreach(var b in s.Bullets)html.Append("<li>"+H(b)+"</li>");html.Append("</ul><p class='refs'>"+H(string.Join(" · ",s.SourceIds.Select(id=>sources.First(x=>x.Id==id).Label)))+"</p></section>");}
  if(images.Count>0)foreach(var slide in images)html.Append("<figure class='slide'><h2>"+H(slide.Label)+"</h2><p class='warning'>"+H(Label(language,"images"))+"</p><img src='data:image/jpeg;base64,"+slide.Image+"'></figure>");
  html.Append("<section class='sources'><h2>"+H(Label(language,"sources"))+"</h2>");foreach(var source in sources)html.Append("<p><b>"+H(source.Id+" · "+source.Label)+"</b><br>"+H(source.Text)+"</p>");
  return html.Append("</section></html>").ToString();
 }
 static string Label(string language,string key)
 {
  using var registry=JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"languages.json")));
  return registry.RootElement.GetProperty(language).GetProperty("pdfLabels").GetProperty(key).GetString()!;
 }
 public async Task<string> Render(StudyNotes notes,IReadOnlyList<NoteSource> sources,NotesPreferences preferences,string sessionFolder,string outputFolder,CancellationToken cancellation)
 {
  NotesValidation.Validate(notes,sources);
  var images=new List<(string,string)>();var slideFolder=Path.Combine(sessionFolder,"slides");
  if(preferences.IncludeSlides&&Directory.Exists(slideFolder)){
   var files=Directory.GetFiles(slideFolder,"*.slide.json").Order().ToArray();if(files.Length>100)throw new InvalidDataException("This preview supports up to 100 slide images. Export without images for larger sessions.");
   foreach(var file in files){cancellation.ThrowIfCancellationRequested();using var slide=JsonDocument.Parse(await File.ReadAllTextAsync(file,cancellation));var root=slide.RootElement;var bytes=Convert.FromBase64String(root.GetProperty("data").GetString()!);if(bytes.Length>500000||Convert.ToHexStringLower(SHA256.HashData(bytes))!=root.GetProperty("sha256").GetString())throw new InvalidDataException("Slide integrity failure");images.Add(((root.GetProperty("slide").GetInt32()+1)+" · "+TimeSpan.FromMilliseconds(root.GetProperty("atMs").GetDouble()).ToString(@"hh\:mm\:ss"),Convert.ToBase64String(bytes)));}
  }
  Directory.CreateDirectory(outputFolder);var html=Path.Combine(outputFolder,"notes.html");var pdf=Path.Combine(outputFolder,"notes.pdf");
  await File.WriteAllTextAsync(html,Compose(notes,sources,preferences.Language,preferences.Detail,images),cancellation);
  var browsers=new[]{Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),"Microsoft","Edge","Application","msedge.exe"),Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),"Google","Chrome","Application","chrome.exe")};
  var browser=browsers.FirstOrDefault(File.Exists)??throw new FileNotFoundException("PDF export needs installed Microsoft Edge or Chrome. HTML was saved.");
  var psi=new ProcessStartInfo(browser){UseShellExecute=false,CreateNoWindow=true};
  foreach(var arg in new[]{"--headless=new","--disable-gpu","--no-first-run","--no-default-browser-check","--disable-background-networking","--no-pdf-header-footer","--user-data-dir="+Path.Combine(outputFolder,"browser-profile"),"--print-to-pdf="+pdf,new Uri(html).AbsoluteUri})psi.ArgumentList.Add(arg);
  using var process=Process.Start(psi)??throw new IOException("PDF renderer could not start");
  using var deadline=CancellationTokenSource.CreateLinkedTokenSource(cancellation);deadline.CancelAfter(TimeSpan.FromSeconds(60));
  try{await process.WaitForExitAsync(deadline.Token);}catch{if(!process.HasExited)process.Kill(true);throw;}
  if(!File.Exists(pdf)||new FileInfo(pdf).Length<100)throw new IOException("PDF export failed; HTML is available in the output folder");
  return pdf;
 }
}
