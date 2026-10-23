using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text.Json;

namespace LectureLens.Desktop.Adapters;

internal sealed class SpeechAssets
{
 internal record Asset(string Url,string Sha256,string File);
 readonly string root;
 readonly Dictionary<string,Asset> assets;
 internal SpeechAssets()
 {
  var candidate=new DirectoryInfo(AppContext.BaseDirectory);
  while(candidate!=null&&!Directory.Exists(Path.Combine(candidate.FullName,".tools","speech")))candidate=candidate.Parent;
  root=candidate!=null?Path.Combine(candidate.FullName,".tools","speech"):Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"LectureLens","speech");
  var config=Path.Combine(AppContext.BaseDirectory,"speech-assets.json");
  assets=JsonSerializer.Deserialize<Dictionary<string,Asset>>(File.ReadAllText(config),new JsonSerializerOptions{PropertyNameCaseInsensitive=true})!;
 }
 internal string Engine=>Path.Combine(root,"engine","Release","whisper-cli.exe");
 internal string Model=>Path.Combine(root,"small-q5.bin");
 internal string Vad=>Path.Combine(root,"silero.bin");
 internal string Decoder=>Directory.EnumerateFiles(Path.Combine(root,"decoder"),"ffmpeg.exe",SearchOption.AllDirectories).Single();
 internal string Version=>string.Join("/",new[]{"small","engine","decoder","vad"}.Select(k=>assets[k].Sha256));
 internal async Task EnsureReady(IProgress<string>? progress,CancellationToken cancel)
 {
  Directory.CreateDirectory(root);
  using var http=new HttpClient{Timeout=TimeSpan.FromMinutes(30)};
  foreach(var key in new[]{"engine","small","vad","decoder"})
  {
   cancel.ThrowIfCancellationRequested();var asset=assets[key];var path=Path.Combine(root,asset.File);
   if(!await Matches(path,asset.Sha256,cancel))
   {
    progress?.Report("Downloading local speech tools: "+key+". Your lesson stays on this PC.");
    using var response=await http.GetAsync(asset.Url,HttpCompletionOption.ResponseHeadersRead,cancel);response.EnsureSuccessStatusCode();
    await using(var output=new FileStream(path+".partial",FileMode.Create,FileAccess.Write,FileShare.None,81920,true))
    await using(var input=await response.Content.ReadAsStreamAsync(cancel))
    {
     var buffer=new byte[81920];long copied=0,lastReported=0;int read;while((read=await input.ReadAsync(buffer,cancel))>0){await output.WriteAsync(buffer.AsMemory(0,read),cancel);copied+=read;if(copied-lastReported>=5_000_000){lastReported=copied;progress?.Report("Downloading "+key+": "+(copied/1_000_000)+" MB");}}
    }
    if(!await Matches(path+".partial",asset.Sha256,cancel))throw new InvalidDataException("Speech tool checksum mismatch. Download was not installed.");
    File.Move(path+".partial",path,true);
   }
   if(key is "engine" or "decoder")
   {
    var target=Path.Combine(root,key);var marker=Path.Combine(target,"verified.sha256");
    if(!File.Exists(marker)||await File.ReadAllTextAsync(marker,cancel)!=asset.Sha256)
    {
     progress?.Report("Installing local speech tools: "+key);
     await Task.Run(()=>{cancel.ThrowIfCancellationRequested();ZipFile.ExtractToDirectory(path,target,true);},cancel);
     await File.WriteAllTextAsync(marker,asset.Sha256,cancel);
    }
   }
  }
  if(!File.Exists(Engine)||!File.Exists(Decoder))throw new FileNotFoundException("Speech setup is incomplete. Retry transcription to repair it.");
 }
 static async Task<bool> Matches(string path,string expected,CancellationToken cancel)
 {
  if(!File.Exists(path))return false;
  await using var stream=File.OpenRead(path);
  return Convert.ToHexStringLower(await SHA256.HashDataAsync(stream,cancel))==expected;
 }
}
