using System.IO;
using System.Text.Json;
using LectureLens.Desktop.Adapters;

internal static class TimestampChecks
{
    const string Response = """
        {"transcription":[
          {"offsets":{"from":0,"to":4100},"text":"First explanation"},
          {"offsets":{"from":55160,"to":85140},"text":"Final explanation"},
          {"offsets":{"from":70000,"to":90000},"text":"Outside recording"},
          {"offsets":{"from":4000,"to":2000},"text":"Reversed"},
          {"offsets":{"from":-20,"to":1000},"text":"Rounded start"}
        ]}
        """;
    internal static async Task Run(string root)
    {
        using var json = JsonDocument.Parse(Response);
        var segments = LocalSpeech.Normalize(json.RootElement, 65.16, 180, "en");
        Check(segments[0].Start == 180 && segments[0].End == 184.1 && segments[0].Kept, "valid timing stays unchanged");
        Check(segments[1].Start == 235.16 && segments[1].End == 245.16 && segments[1].Kept && segments[1].Reason.Contains("timestamp-clipped"), "provider overrun clipped and flagged without losing text");
        Check(!segments[2].Kept && !segments[3].Kept, "outside and reversed timestamps rejected per segment");
        Check(segments[4].Start == 180 && segments[4].Kept, "overlapping negative start clipped");
        Check(segments.All(s => s.Start >= 180 && s.End <= 245.16 && s.End >= s.Start), "all normalized timing stays within audio");
        var folder = Path.Combine(root, "timestamp-resume"); Directory.CreateDirectory(folder);
        await File.WriteAllTextAsync(Path.Combine(folder, "audio.webm"), "mock recording");
        var runtime = new Runtime(); var service = new LocalSpeech(runtime);
        try { await service.Transcribe(folder, "en", null, CancellationToken.None); throw new Exception("Expected interrupted checkpoint"); }
        catch (IOException) { }
        var final = await service.Transcribe(folder, "en", null, CancellationToken.None);
        Check(runtime.Calls == 1, "retry reuses durable provider response without upload");
        using var result = JsonDocument.Parse(await File.ReadAllTextAsync(final));
        Check(result.RootElement.GetProperty("segments").GetArrayLength() == 5, "retry publishes complete decisions including rejected segments");
    }
    static void Check(bool passed, string label) { if (!passed) throw new Exception(label); Console.WriteLine("PASS " + label); }
    sealed class Runtime : ILocalSpeechRuntime
    {
        internal int Calls;
        public string Version => "timestamp-fixture";
        public Task EnsureReady(IProgress<string>? progress, CancellationToken cancel) => Task.CompletedTask;
        public Task<DecodedSpeechAudio> Decode(string input, string log, CancellationToken cancel)
        {
            var stream = new MemoryStream(new byte[2085120]);
            return Task.FromResult(new DecodedSpeechAudio(stream, () => Task.CompletedTask, stream.DisposeAsync));
        }
        public async Task TranscribeChunk(string input, string output, string language, string log, CancellationToken cancel)
        {
            Calls++;
            await File.WriteAllTextAsync(output + ".json", Response, cancel);
            throw new IOException("Simulate interruption after provider response was saved");
        }
    }
}
