using System.IO;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text.Json;
using LectureLens.Core;
using LectureLens.Desktop;
using LectureLens.Desktop.Adapters;

internal static class CloudAndTimelineChecks
{
 internal static async Task Run(string root)
 {
  await NotesFormatChecks.Run(root);
  await TimestampChecks.Run(root);
  static void Check(bool ok,string label){if(!ok)throw new Exception(label);Console.WriteLine("PASS "+label);}
  var wav=Path.Combine(root,"chunk.wav");await LocalSpeech.WriteWav(wav,new byte[32000],CancellationToken.None);
  var handler=new SpeechHandler();using var http=new HttpClient(handler);var output=Path.Combine(root,"cloud");
  try{await new GroqSpeech(false,http).TranscribeChunk(wav,output,"fr","unused",CancellationToken.None);throw new Exception("consent bypassed");}catch(InvalidOperationException){}
  Check(handler.Calls==0,"no audio request without explicit consent");
  await new GroqSpeech(true,http).TranscribeChunk(wav,output,"fr","unused",CancellationToken.None);
  using(var json=JsonDocument.Parse(await File.ReadAllTextAsync(output+".json"))){var segments=LocalSpeech.Normalize(json.RootElement,1,180,"fr");Check(segments[0].Start==180&&segments[0].End==181&&segments[0].Text=="Cours","cloud timing uses the same resumable audio clock");Check(segments[0].Confidence is >0.8 and <=1,"cloud quality signal reaches the transcript without inventing certainty");}
  Check(handler.ValidRequest,"speech request uses multilingual model, language, timestamps and bounded WAV");
  handler.Quota=true;
  try{await new GroqSpeech(true,http).TranscribeChunk(wav,output,"fr","unused",CancellationToken.None);throw new Exception("quota ignored");}catch(HttpRequestException){}
  Check(handler.Calls==2,"long provider quota delay stops without retry storm");
  using(var cancellation=new CancellationTokenSource()){cancellation.Cancel();try{await new GroqSpeech(true,http).TranscribeChunk(wav,output,"fr","unused",cancellation.Token);throw new Exception("cancel ignored");}catch(OperationCanceledException){}Check(handler.Calls==2,"cancel prevents upload");}

  var folder=Path.Combine(root,"timeline");Directory.CreateDirectory(Path.Combine(folder,"slides"));
  byte[] jpeg=[255,216,255,217];
  for(int i=0;i<3;i++)await File.WriteAllTextAsync(Path.Combine(folder,"slides",$"{i:D5}.slide.json"),JsonSerializer.Serialize(new{slide=i,atMs=i*20000,data=Convert.ToBase64String(jpeg),sha256=Convert.ToHexStringLower(SHA256.HashData(jpeg))}));
  await File.WriteAllTextAsync(Path.Combine(folder,"events.ndjson"),"{\"type\":\"started\",\"atMs\":0}\n{\"type\":\"paused\",\"atMs\":10000}\n{\"type\":\"resumed\",\"atMs\":15000}\n");
  NoteSource[] sources=[new("S1","first","First concept",1,5,"decoded-audio"),new("S2","second","Second concept",16,19,"decoded-audio"),new("S3","third","Third concept",36,39,"decoded-audio"),new("S4","import","Untimed text")];
  var aligned=await SlideTimeline.Align(folder,sources,CancellationToken.None);
  Check(aligned.Select(s=>s.SlideId).SequenceEqual(new int?[]{0,1,2,null}),"slide alignment subtracts pauses and never guesses imported-text timing");
  var parts=SlideTimeline.Parts(aligned);
  Check(parts.Count==4&&parts[2][0].Id=="S3","generation visits later slides in separate chronological requests");
  var notes=new StudyNotes("Lesson","",aligned.Select(s=>new NoteSection(s.Text,["Explain "+s.Text],[s.Id])).ToArray());
  var html=Path.Combine(folder,"notes.html");
  await BrowserPdf.WriteHtml(notes,aligned,new("standard","en",true),folder,html,CancellationToken.None);
  var text=await File.ReadAllTextAsync(html);var pages=text.Split("<article class='lesson-slide'>");
  Check(pages.Length==4&&pages[1].Contains("Explain First concept")&&!pages[1].Contains("Explain Second concept")&&pages[2].Contains("Explain Second concept")&&pages[3].Contains("Explain Third concept"),"each image receives its own explanation, including the final slide");
  Check(!text.Contains("class='refs'")&&!text.Contains("S1 ·"),"student PDF omits internal source IDs and reference clutter");
  var transcript=Path.Combine(folder,"import.json");
  await File.WriteAllTextAsync(transcript,"{\"timestampBasis\":\"decoded-audio\",\"segments\":[{\"start\":2,\"end\":4,\"text\":\"timed\",\"kept\":true}]}");
  var imported=LessonTranscript.Import(transcript);Check(imported[0].Start==2&&imported[0].End==4&&imported[0].TimestampBasis=="decoded-audio","import retains numeric timing for future exports");
 }
 sealed class SpeechHandler:HttpMessageHandler
 {
  internal int Calls;internal bool ValidRequest,Quota;
  protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken cancel)
  {
   Calls++;var body=await request.Content!.ReadAsStringAsync(cancel);
   ValidRequest=request.RequestUri!.Host=="api.groq.com"&&body.Contains("whisper-large-v3")&&body.Contains("verbose_json")&&body.Contains("RIFF")&&body.Contains("fr");
   if(Quota){var response=new HttpResponseMessage(HttpStatusCode.TooManyRequests);response.Headers.RetryAfter=new(TimeSpan.FromHours(1));return response;}
   return new(HttpStatusCode.OK){Content=new StringContent("{\"segments\":[{\"start\":0,\"end\":1,\"text\":\"Cours\",\"no_speech_prob\":0.01,\"avg_logprob\":-0.1}]}")};
  }
 }
}
