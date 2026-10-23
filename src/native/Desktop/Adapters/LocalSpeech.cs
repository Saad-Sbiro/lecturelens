using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace LectureLens.Desktop.Adapters;

// Decoding is streamed and inference is checkpointed: a two-hour class never becomes one huge PCM allocation.
internal sealed class LocalSpeech
{
 internal const int SampleRate=16000,ChunkSeconds=180,BytesPerSecond=SampleRate*2;
 internal record Segment(double Start,double End,string Text,string Language,double? Confidence,bool Kept,string Reason);
 internal record Transcript(string Fingerprint,string Language,double AudioSeconds,string TimestampBasis,string Quality,Segment[] Segments);
 static readonly JsonSerializerOptions Json=new(){PropertyNamingPolicy=JsonNamingPolicy.CamelCase,PropertyNameCaseInsensitive=true,WriteIndented=true};
 readonly ILocalSpeechRuntime runtime;
 internal LocalSpeech(ILocalSpeechRuntime? runtime=null){this.runtime=runtime??new LocalSpeechProcess();}
 internal Task EnsureReady(IProgress<string>? progress,CancellationToken cancel)=>runtime.EnsureReady(progress,cancel);
 internal async Task<string> Transcribe(string sessionFolder,string language,IProgress<string>? progress,CancellationToken cancel)
 {
  using var registry=JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory,"languages.json"),cancel));
  if(!registry.RootElement.TryGetProperty(language,out var registered)||!registered.GetProperty("enabled").GetBoolean())throw new InvalidDataException("Choose a supported teacher language.");
  var speechCode=registered.GetProperty("speechCode").GetString()!;
  var audio=new[]{"audio.wav","audio.webm","recording.webm"}.Select(name=>Path.Combine(sessionFolder,name)).FirstOrDefault(File.Exists)??throw new FileNotFoundException("This lesson has no recovered audio. Choose Recover, then try transcription again.");
  var sourceInfo=new FileInfo(audio);var sourceSize=sourceInfo.Length;var sourceWrite=sourceInfo.LastWriteTimeUtc;
  progress?.Report("Preparing saved lesson for transcription…");
  string audioHash;await using(var source=File.OpenRead(audio))audioHash=Convert.ToHexStringLower(await SHA256.HashDataAsync(source,cancel));
  var fingerprint=Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes("stream-chunks-v1/"+runtime.Version+"/"+speechCode+"/"+audioHash)));
  var final=Path.Combine(sessionFolder,"speech-transcript.json");
  if(File.Exists(final))try{var cached=JsonSerializer.Deserialize<Transcript>(await File.ReadAllTextAsync(final,cancel),Json);if(cached?.Fingerprint==fingerprint&&Valid(cached.Segments,cached.AudioSeconds)){progress?.Report("Saved transcript is ready.");return final;}}catch(JsonException){}
  await EnsureReady(progress,cancel);
  var work=Path.Combine(sessionFolder,"speech-work",fingerprint);Directory.CreateDirectory(work);
  var segments=new List<Segment>();long decodedBytes=0;int chunk=0;
  await using(var decoded=await runtime.Decode(audio,Path.Combine(work,"decoder.log"),cancel))
  {
   var buffer=new byte[ChunkSeconds*BytesPerSecond];int count;
   while((count=await Fill(decoded.Audio,buffer,cancel))>0)
   {
    cancel.ThrowIfCancellationRequested();if(count%2!=0)throw new InvalidDataException("The decoded audio ended inside a sample.");
    var offset=(double)decodedBytes/BytesPerSecond;var duration=(double)count/BytesPerSecond;decodedBytes+=count;chunk++;
    var stem=Path.Combine(work,chunk.ToString("D5",CultureInfo.InvariantCulture));var checkpoint=stem+".segments.json";
    Segment[]? part=null;
    if(File.Exists(checkpoint))try{var previous=JsonSerializer.Deserialize<Segment[]>(await File.ReadAllTextAsync(checkpoint,cancel),Json);if(previous!=null&&Valid(previous,offset+duration)&&previous.All(s=>s.Start>=offset))part=previous;}catch(JsonException){}
    if(part==null)
    {
     progress?.Report("Transcribing · "+TimeSpan.FromSeconds(offset).ToString(@"hh\:mm\:ss")+"–"+TimeSpan.FromSeconds(offset+duration).ToString(@"hh\:mm\:ss")+" · part "+chunk);
     var wav=stem+".wav";
     try
     {
      await WriteWav(wav,buffer.AsMemory(0,count),cancel);
      await runtime.TranscribeChunk(wav,stem,speechCode,stem+".log",cancel);
      using var result=JsonDocument.Parse(await File.ReadAllTextAsync(stem+".json",cancel));
      part=Normalize(result.RootElement,duration,offset,language);
      await Atomic(checkpoint,JsonSerializer.Serialize(part,Json),cancel);
     }
     finally{if(File.Exists(wav))File.Delete(wav);}
    }
    else progress?.Report("Resuming saved transcript · part "+chunk);
    segments.AddRange(part);
   }
   await decoded.Complete();
  }
  sourceInfo.Refresh();if(sourceInfo.Length!=sourceSize||sourceInfo.LastWriteTimeUtc!=sourceWrite)throw new IOException("The recording changed during transcription. Wait until recording has stopped, then retry.");
  if(decodedBytes==0)throw new InvalidDataException("This recording contains no decodable audio.");
  var report=new Transcript(fingerprint,language,(double)decodedBytes/BytesPerSecond,"decoded-audio; recording pauses are excluded, not aligned to slide timestamps","unreviewed; check names and technical terms",segments.ToArray());
  await Atomic(final,JsonSerializer.Serialize(report,Json),cancel);
  await Atomic(Path.Combine(sessionFolder,"speech-transcript.txt"),string.Join(Environment.NewLine,segments.Where(s=>s.Kept).Select(s=>"["+TimeSpan.FromSeconds(s.Start).ToString(@"hh\:mm\:ss")+"] "+s.Text)),cancel);
  progress?.Report(segments.Any(s=>s.Kept)?"Transcript saved. Review technical terms before generating notes.":"No speech detected. Check the recording audio and teacher language.");return final;
 }
 internal static async Task<int> Fill(Stream input,Memory<byte> buffer,CancellationToken cancel)
 {
  int count=0,read;while(count<buffer.Length&&(read=await input.ReadAsync(buffer[count..],cancel))>0)count+=read;return count;
 }
 internal static Segment[] Normalize(JsonElement result,double duration,double offset,string language)
 {
  if(!result.TryGetProperty("transcription",out var entries)||entries.ValueKind!=JsonValueKind.Array)throw new InvalidDataException("Speech worker returned no segment list.");
  var output=new List<Segment>();foreach(var s in entries.EnumerateArray())
  {
   var start=s.GetProperty("offsets").GetProperty("from").GetDouble()/1000;var end=s.GetProperty("offsets").GetProperty("to").GetDouble()/1000;var text=s.GetProperty("text").GetString()?.Trim()??"";
   if(!double.IsFinite(start)||!double.IsFinite(end)||start<0||end<start||end>duration+1)throw new InvalidDataException("Speech worker returned an invalid timestamp.");
   var silent=s.TryGetProperty("noSpeech",out var ns)&&ns.ValueKind==JsonValueKind.Number&&ns.GetDouble()>0.8;
   double? confidence=null;if(s.TryGetProperty("avgLogprob",out var lp)&&lp.ValueKind==JsonValueKind.Number&&double.IsFinite(lp.GetDouble()))confidence=Math.Clamp(Math.Exp(lp.GetDouble()),0,1);
   output.Add(new(offset+Math.Min(start,duration),offset+Math.Min(end,duration),text,language,confidence,text.Length>0&&!silent,silent?"likely-no-speech":text.Length>0?"transcribed-unreviewed":"empty-segment"));
  }
  return output.ToArray();
 }
 static bool Valid(Segment[] segments,double duration)=>segments.All(s=>double.IsFinite(s.Start)&&double.IsFinite(s.End)&&s.Start>=0&&s.End>=s.Start&&s.End<=duration&&s.Text!=null&&s.Reason!=null);
 internal static async Task WriteWav(string path,ReadOnlyMemory<byte> pcm,CancellationToken cancel)
 {
  await using var output=new FileStream(path,FileMode.Create,FileAccess.Write,FileShare.None,81920,true);
  using(var header=new BinaryWriter(output,Encoding.ASCII,true)){header.Write(Encoding.ASCII.GetBytes("RIFF"));header.Write(36+pcm.Length);header.Write(Encoding.ASCII.GetBytes("WAVEfmt "));header.Write(16);header.Write((short)1);header.Write((short)1);header.Write(SampleRate);header.Write(BytesPerSecond);header.Write((short)2);header.Write((short)16);header.Write(Encoding.ASCII.GetBytes("data"));header.Write(pcm.Length);}
  await output.WriteAsync(pcm,cancel);
 }
 static async Task Atomic(string path,string content,CancellationToken cancel){await File.WriteAllTextAsync(path+".partial",content,cancel);cancel.ThrowIfCancellationRequested();File.Move(path+".partial",path,true);}
}
