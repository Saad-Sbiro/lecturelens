using System.Globalization;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;

namespace LectureLens.Desktop.Adapters;

// Shares streaming decode and durable chunk checkpoints with offline speech; never loads local inference models.
internal sealed class GroqSpeech(bool audioConsent, HttpClient? injected = null) : ILocalSpeechRuntime
{
 internal const string Model = "whisper-large-v3";
 public string Version => "groq/" + Model + "/v1";
 public Task EnsureReady(IProgress<string>? progress, CancellationToken cancel)
 {
  Authorize();
  progress?.Report("Preparing cloud transcription. Only audio is sent to Groq; images stay on this PC.");
  return new SpeechAssets().EnsureDecoder(progress, cancel);
 }
 void Authorize()
 {
  if (!audioConsent) throw new InvalidOperationException("Audio upload consent is required for this transcription.");
  if (injected is null && !KeyVault.HasSpeechKey) throw new InvalidOperationException("Add a Groq API key in AI connections first.");
 }
 public Task<DecodedSpeechAudio> Decode(string input, string log, CancellationToken cancel) => new LocalSpeechProcess().Decode(input, log, cancel);
 public async Task TranscribeChunk(string input, string output, string language, string log, CancellationToken cancel)
 {
  Authorize(); cancel.ThrowIfCancellationRequested();
  if (new FileInfo(input).Length > 24_000_000) throw new InvalidDataException("Speech upload exceeds the free-plan file limit.");
  using var owned = injected is null ? new HttpClient { Timeout = TimeSpan.FromMinutes(3) } : null;
  var http = injected ?? owned!;
  for (int attempt = 0; attempt < 3; attempt++) {
   using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.groq.com/openai/v1/audio/transcriptions");
   request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", injected is null ? KeyVault.Read(true) : "mock");
   using var form = new MultipartFormDataContent();
   var audio = new StreamContent(File.OpenRead(input)); audio.Headers.ContentType = new MediaTypeHeaderValue("audio/wav");
   form.Add(audio, "file", "chunk.wav"); form.Add(new StringContent(Model), "model");
   form.Add(new StringContent(language), "language"); form.Add(new StringContent("verbose_json"), "response_format");
   form.Add(new StringContent("0"), "temperature"); request.Content = form;
   using var response = await http.SendAsync(request, cancel);
   if ((int)response.StatusCode == 429 || (int)response.StatusCode >= 500) {
    var delay = response.Headers.RetryAfter?.Delta ?? (response.Headers.RetryAfter?.Date - DateTimeOffset.UtcNow) ?? TimeSpan.FromSeconds(3 * (attempt + 1));
    if (attempt < 2 && delay <= TimeSpan.FromSeconds(20)) { await Task.Delay(delay < TimeSpan.Zero ? TimeSpan.Zero : delay, cancel); continue; }
    throw new HttpRequestException("Speech service quota or availability limit. Completed parts are saved; retry later. No paid upgrade was requested.");
   }
   if (!response.IsSuccessStatusCode) throw new HttpRequestException("Speech service HTTP " + (int)response.StatusCode + ". Check the Groq key and account limits; completed parts are saved.");
   using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancel));
   var entries = json.RootElement.GetProperty("segments").EnumerateArray().Select(s => new {
    offsets = new { from = s.GetProperty("start").GetDouble() * 1000, to = s.GetProperty("end").GetDouble() * 1000 },
    text = s.GetProperty("text").GetString(),
    noSpeech = s.TryGetProperty("no_speech_prob", out var probability) ? probability.GetDouble() : 0,
    avgLogprob = s.TryGetProperty("avg_logprob", out var logProbability) ? logProbability.GetDouble() : (double?)null
   }).ToArray();
   await File.WriteAllTextAsync(output + ".json", JsonSerializer.Serialize(new { transcription = entries }), cancel);
   return;
  }
 }
}
