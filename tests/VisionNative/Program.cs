using System.Text.Json;
using LectureLens.NativeHost;
var root = Path.Combine(Path.GetTempPath(), "lens-vision-" + Guid.NewGuid());
try {
 var handler = new MockHandler(); using var http = new HttpClient(handler);
 var first = JsonSerializer.SerializeToElement(await GeminiVision.Detect("/9j/2Q==", http, root));
 if (first.GetProperty("detection").GetProperty("crop").GetProperty("width").GetDouble() <= .6) throw new Exception("Margin missing");
 await GeminiVision.Detect("/9j/2Q==", http, root);
 if (handler.Calls != 1) throw new Exception("Duplicate request escaped cache");
 var usage = Directory.GetFiles(root, "*.usage.json").Single();
 if (!File.ReadAllText(usage).Contains("123")) throw new Exception("Usage missing");
 handler.Quota = true;
 var quota = JsonSerializer.SerializeToElement(await GeminiVision.Detect("/9j/2g==", http, root));
 if (!quota.GetProperty("disabled").GetBoolean() || handler.Calls != 2) throw new Exception("Quota fallback failed");
 using var invalid = JsonDocument.Parse("{\"box\":[500,0,100,100],\"type\":\"slide\",\"isEducational\":true}");
 try { GeminiVision.Validate(invalid.RootElement); throw new Exception("Invalid box accepted"); } catch (InvalidDataException) { }
 Console.WriteLine("PASS native vision request, margin, cache, token usage, invalid bounds and quota fallback (offline)");
} finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
sealed class MockHandler : HttpMessageHandler {
 internal int Calls; internal bool Quota;
 protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token) {
  Calls++;
  var body = await request.Content!.ReadAsStringAsync(token);
  if (!body.Contains("MEDIA_RESOLUTION_MEDIUM") || !body.Contains("LOW") || !request.RequestUri!.AbsolutePath.Contains("gemini-3.8-flash")) throw new Exception("Wrong model/config");
  if (Quota) return new(System.Net.HttpStatusCode.TooManyRequests);
  var text = "{\"box\":[200,200,800,800],\"type\":\"slide\",\"isEducational\":true}";
  return new(System.Net.HttpStatusCode.OK) { Content = new StringContent(JsonSerializer.Serialize(new { usageMetadata = new { totalTokenCount = 123 }, candidates = new[] { new { finishReason = "STOP", content = new { parts = new[] { new { text } } } } } })) };
 }
}
