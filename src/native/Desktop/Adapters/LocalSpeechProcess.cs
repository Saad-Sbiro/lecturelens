using System.Diagnostics;
using System.IO;

namespace LectureLens.Desktop.Adapters;

internal interface ILocalSpeechRuntime
{
 string Version{get;}
 Task EnsureReady(IProgress<string>? progress,CancellationToken cancel);
 Task<DecodedSpeechAudio> Decode(string input,string log,CancellationToken cancel);
 Task TranscribeChunk(string input,string output,string language,string log,CancellationToken cancel);
}
internal sealed class DecodedSpeechAudio(Stream audio,Func<Task> complete,Func<ValueTask> dispose):IAsyncDisposable
{
 internal Stream Audio=>audio;
 internal Task Complete()=>complete();
 public ValueTask DisposeAsync()=>dispose();
}
internal sealed class LocalSpeechProcess:ILocalSpeechRuntime
{
 readonly SpeechAssets assets=new();
 public string Version=>assets.Version;
 public Task EnsureReady(IProgress<string>? progress,CancellationToken cancel)=>assets.EnsureReady(progress,cancel);
 static Process Start(string exe,IEnumerable<string> args,bool binaryOutput=false)
 {
  var start=new ProcessStartInfo(exe){UseShellExecute=false,CreateNoWindow=true,RedirectStandardError=true,RedirectStandardOutput=true};
  foreach(var arg in args)start.ArgumentList.Add(arg);
  return Process.Start(start)??throw new IOException("Could not start the local speech worker.");
 }
 static async Task Drain(StreamReader reader,string path,CancellationToken cancel)
 {
  await using var file=new FileStream(path,FileMode.Create,FileAccess.Write,FileShare.Read,81920,true);
  await using var writer=new StreamWriter(file);var buffer=new char[4096];long saved=0;int read;
  while((read=await reader.ReadAsync(buffer,cancel))>0){if(saved<2_000_000)await writer.WriteAsync(buffer.AsMemory(0,read),cancel);saved+=read;}
 }
 static void Kill(Process process){try{if(!process.HasExited)process.Kill(true);}catch(InvalidOperationException){} }
 public Task<DecodedSpeechAudio> Decode(string input,string log,CancellationToken cancel)
 {
  var process=Start(assets.Decoder,new[]{"-nostdin","-hide_banner","-loglevel","error","-i",input,"-map","0:a:0","-vn","-ac","1","-ar","16000","-f","s16le","pipe:1"},true);
  var registration=cancel.Register(()=>Kill(process));var error=Drain(process.StandardError,log,cancel);
  async Task Complete(){await process.WaitForExitAsync(cancel);await error;if(process.ExitCode!=0)throw new InvalidDataException("The recording audio could not be decoded. Your original recording is unchanged.");}
  async ValueTask Dispose(){Kill(process);try{await process.WaitForExitAsync();await error;}catch(OperationCanceledException){}finally{registration.Dispose();process.Dispose();}}
  return Task.FromResult(new DecodedSpeechAudio(process.StandardOutput.BaseStream,Complete,Dispose));
 }
 public async Task TranscribeChunk(string input,string output,string language,string log,CancellationToken cancel)
 {
  var args=new[]{"-m",assets.Model,"-f",input,"-l",language,"-t",Math.Clamp(Environment.ProcessorCount/2,1,4).ToString(),"-ng","--vad","-vm",assets.Vad,"-vsd","500","-vp","200","-oj","-of",output};
  using var process=Start(assets.Engine,args);using var timeout=CancellationTokenSource.CreateLinkedTokenSource(cancel);timeout.CancelAfter(TimeSpan.FromMinutes(20));
  using var registration=timeout.Token.Register(()=>Kill(process));var stderr=Drain(process.StandardError,log,timeout.Token);var stdout=Drain(process.StandardOutput,log+".out",timeout.Token);
  try{await process.WaitForExitAsync(timeout.Token);await Task.WhenAll(stderr,stdout);if(process.ExitCode!=0)throw new IOException("Local transcription failed. Completed parts were saved; retry to continue.");}
  catch(OperationCanceledException) when(!cancel.IsCancellationRequested){throw new TimeoutException("A speech chunk took longer than 20 minutes. Completed parts are saved.");}
  finally{Kill(process);await process.WaitForExitAsync();try{await Task.WhenAll(stderr,stdout);}catch(OperationCanceledException){}}
 }
}
