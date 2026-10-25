using System.IO;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using LectureLens.Core;
using LectureLens.Desktop.Adapters;

internal static class NotesFormatChecks
{
 internal static async Task Run(string root)
 {
  var sources = new[] { new NoteSource("S1", "00:00:00", "Fixture source.") };
  var prefs = new NotesPreferences("standard", "fr", false);
  var valid = JsonSerializer.Serialize(new StudyNotes("Fixture", "Overview", new[] { new NoteSection("Topic", [], ["S1"], ["A fact explained in context."]) }, [new("S1","keep","Educational explanation")]));
  const string malformed = "{\"title\":\"Fixture\",\"overview\":\"Overview\",\"sections\":[{\"title\":\"Topic\",\"bullets\":[\"a\":\"b\"],\"sourceIds\":[\"S1\"]}]}";
  var handler = new Replies(malformed, valid);
  using var http = new HttpClient(handler);
  var cache = Path.Combine(root, "format-recovery");
  var adapter = new NvidiaNotes("fixture", cache, http);
  await adapter.Generate(sources, prefs, CancellationToken.None);
  await adapter.Generate(sources, prefs, CancellationToken.None);
  if(handler.Calls != 2 || Directory.GetFiles(cache, "*.json").Length != 1) throw new Exception("Format recovery/cache failed");
  var bad = new Replies(malformed, malformed, malformed);
  using var badHttp = new HttpClient(bad);
  var badCache = Path.Combine(root, "format-failure");
  try {
   await new NvidiaNotes("fixture", badCache, badHttp).Generate(sources, prefs, CancellationToken.None);
   throw new Exception("Malformed notes accepted");
  } catch(InvalidDataException e) when(e.Message.Contains("three attempts")) { }
  if(bad.Calls != 3 || Directory.GetFiles(badCache).Length != 0) throw new Exception("Retry bound or invalid cache failed");
  Console.WriteLine("PASS malformed notes regenerate from sources, cache only validated output, and stop after three attempts");
 }
 sealed class Replies(params string[] replies) : HttpMessageHandler
 {
  internal int Calls;
  protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
  {
   var body = await request.Content!.ReadAsStringAsync(token);
   if(Calls > 0 && !body.Contains("previous response failed")) throw new Exception("Missing correction instruction");
   return new(HttpStatusCode.OK) { Content = new StringContent(JsonSerializer.Serialize(new { choices = new[] { new { finish_reason = "stop", message = new { content = replies[Calls++] } } } })) };
  }
 }
}
