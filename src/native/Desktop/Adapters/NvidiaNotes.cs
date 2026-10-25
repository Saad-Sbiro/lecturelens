using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using LectureLens.Core;
namespace LectureLens.Desktop.Adapters;
internal sealed class NvidiaNotes(string model, string cacheFolder, HttpClient? injected = null) : ITextGenerator
{
 internal const string DefaultModel="nvidia/nemotron-3.5-super-vl-120b-a12b";
 internal static readonly JsonSerializerOptions Json=new(){PropertyNameCaseInsensitive=true,WriteIndented=true};
 public async Task<StudyNotes> Generate(IReadOnlyList<NoteSource> sources, NotesPreferences preferences, CancellationToken cancellation)
 {
  var content=JsonSerializer.Serialize(new { lessonTopic=preferences.Topic, sources });
  var instruction=LessonNotesPrompt.Instructions+" Output language: "+preferences.Language+". Detail level: "+preferences.Detail+".";
  using var registry=JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"languages.json")));
  if(registry.RootElement.GetProperty(preferences.Language).TryGetProperty("notesInstructions",out var localeNotes))instruction+=" "+localeNotes.GetString();
  string Request(string correction="") => JsonSerializer.Serialize(new{model,messages=new[]{new{role="system",content=instruction+correction},new{role="user",content}},temperature=0.2,max_tokens=preferences.Detail=="detailed"?8500:6500,stream=false,chat_template_kwargs=new{enable_thinking=false}});
  var requestJson=Request();
  Directory.CreateDirectory(cacheFolder);
  var cache=Path.Combine(cacheFolder,Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(requestJson)))+".json");
  if(File.Exists(cache)){var stored=JsonSerializer.Deserialize<StudyNotes>(await File.ReadAllTextAsync(cache,cancellation),Json)!;NotesValidation.ValidateLesson(stored,sources);return stored;}
  using var owned=injected is null?new HttpClient{Timeout=TimeSpan.FromSeconds(100)}:null;
  var http=injected??owned!;
  for(var attempt=0;attempt<3;attempt++)
  {
   using var request=new HttpRequestMessage(HttpMethod.Post,"https://integrate.api.nvidia.com/v1/chat/completions");
   request.Headers.Authorization=new AuthenticationHeaderValue("Bearer",injected is null?KeyVault.Read():"mock");
   request.Content=new StringContent(requestJson,Encoding.UTF8,"application/json");
   using var response=await http.SendAsync(request,cancellation);
   if((int)response.StatusCode==429||(int)response.StatusCode>=500){if(attempt<2){var delay=response.Headers.RetryAfter?.Delta??TimeSpan.FromSeconds(2*(attempt+1));if(delay>TimeSpan.FromSeconds(20))throw new HttpRequestException("Provider quota reached. Retry later; completed parts remain cached.");await Task.Delay(delay,cancellation);continue;}}
   if(!response.IsSuccessStatusCode)throw new HttpRequestException("NVIDIA HTTP "+(int)response.StatusCode+". Check the key, model access or quota.");
   using var body=JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellation));
   var choice=body.RootElement.GetProperty("choices")[0];
   if(choice.TryGetProperty("finish_reason",out var finish)&&finish.GetString()=="length")throw new InvalidDataException("Response was truncated. Choose a shorter detail level.");
   var raw=choice.GetProperty("message").GetProperty("content").GetString()!.Trim();
   if(raw.StartsWith("```")){var first=raw.IndexOf('\n');var last=raw.LastIndexOf("```",StringComparison.Ordinal);if(first>=0&&last>first)raw=raw[(first+1)..last];}
   StudyNotes notes;
   try {
    notes=JsonSerializer.Deserialize<StudyNotes>(raw,Json)??throw new InvalidDataException("Empty notes response");
    NotesValidation.ValidateLesson(notes,sources);
   } catch(Exception error) when(error is JsonException or InvalidDataException) {
    // Regenerate from the original sources rather than guessing how malformed
    // punctuation should change the teacher's words. Never cache invalid notes.
    if(attempt==2)throw new InvalidDataException("The notes model returned invalid formatting after three attempts. Generate again; completed lesson parts remain saved.",error);
    requestJson=Request(" Your previous response failed JSON/schema validation. Regenerate from the same sources. Use double-quoted JSON strings, escape quotes inside text, separate items with commas, and make paragraphs, bullets and sourceIds arrays of strings. Include exactly one decision per source and explanatory paragraphs in every section. Cite only the provided source IDs. Return one complete valid JSON object.");
    continue;
   }
   await File.WriteAllTextAsync(cache+".partial",JsonSerializer.Serialize(notes,Json),cancellation);File.Move(cache+".partial",cache,true);
   return notes;
  }
  throw new HttpRequestException("Provider unavailable; retry later.");
 }
}
