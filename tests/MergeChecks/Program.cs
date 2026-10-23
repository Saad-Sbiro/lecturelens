using System.IO;
using System.Text.Json;
using LectureLens.Core;
using LectureLens.Storage;
using LectureLens.Desktop;
using LectureLens.Desktop.Adapters;

internal static class Program
{
    [STAThread]
    static int Main()
    {
        try { Run().GetAwaiter().GetResult(); UiChecks.Run(); return 0; }
        catch (Exception e) { Console.Error.WriteLine(e); return 1; }
    }
    static void Check(bool passed, string label) { if (!passed) throw new Exception(label); Console.WriteLine("PASS " + label); }
    static async Task Run()
    {
        var root = Path.GetFullPath(Path.Combine("artifacts", "merge-checks-" + Guid.NewGuid().ToString("N")));
        Directory.CreateDirectory(root);
        using var store = new LocalStore(root);
        using var crop = JsonDocument.Parse("{\"x\":0,\"y\":0,\"width\":1,\"height\":1}");
        var ids = new List<string>();
        for (byte marker = 1; marker <= 3; marker++) {
            var id = store.Create("audio/webm;codecs=opus"); ids.Add(id);
            store.Append(id, new MediaPacket(0, 0, [marker, marker], "fixture"));
            store.Event(id, "started", 0, "fixture"); store.Event(id, "paused", 500, "fixture"); store.Event(id, "resumed", 1500, "fixture");
            store.SaveSlide(id, 0, 1750, crop.RootElement, "fixture-change", [255, 216, marker, 255, 217]);
            store.Finish(id, "user-stop"); store.Rename(id, "Fixture " + marker);
        }
        var originals = ids.Select(id => File.ReadAllBytes(Path.Combine(root, id, "00000000.chunk"))).ToArray();
        var reordered = new[] { ids[2], ids[0], ids[1] };
        var merged = await new RecordingMerge(new FakeDecoder()).Run(root, reordered, "Ordered lesson", null, CancellationToken.None);
        var folder = Path.Combine(root, merged);
        var wav = File.ReadAllBytes(Path.Combine(folder, "audio.wav"));
        Check(wav.Length == 44 + 3 * 32000 && System.Text.Encoding.ASCII.GetString(wav, 0, 4) == "RIFF", "valid PCM WAV length and container");
        Check(wav[44] == 3 && wav[32044] == 1 && wav[64044] == 2, "three audio parts follow the chosen order");
        for (int i = 0; i < 3; i++) {
            using var slide = JsonDocument.Parse(File.ReadAllText(Path.Combine(folder, "slides", $"{i:D5}.slide.json")));
            Check(slide.RootElement.GetProperty("atMs").GetDouble() == i * 1000 + 750, "slide order and pause-adjusted offset " + i);
            Check(File.ReadAllBytes(Path.Combine(root, ids[i], "00000000.chunk")).SequenceEqual(originals[i]), "original recording preserved " + i);
        }
        using var reopened = new LocalStore(root);
        Check(reopened.List().Single(s => s.Id == merged).Title == "Ordered lesson", "merged lesson persists after reopening storage");
        SlideSelection.Save(folder, [1]);
        Check(SlideSelection.Load(folder).SetEquals([1]) && Directory.GetFiles(Path.Combine(folder, "slides"), "*.slide.json").Length == 3, "soft slide removal preserves captured files");
        SlideSelection.Save(folder, []);
        Check(SlideSelection.Load(folder).Count == 0, "removed slide can be restored");
        File.Delete(Path.Combine(folder, "audio.wav"));
        Check(File.ReadAllBytes(reopened.Recover(merged)).SequenceEqual(wav), "merged audio recovers exactly from durable packets");
        var pair = await new RecordingMerge(new FakeDecoder()).Run(root, ids.Take(2).ToArray(), "Pair", null, CancellationToken.None);
        Check(File.ReadAllBytes(Path.Combine(root, pair, "audio.wav")).Length == 64044, "two-part merge works");
        async Task Reject(IReadOnlyList<string> selected, ILocalSpeechRuntime decoder, CancellationToken token) {
            var before = store.List().Count;
            try { await new RecordingMerge(decoder).Run(root, selected, "Rejected", null, token); throw new Exception("Invalid merge accepted"); }
            catch (Exception e) when (e is InvalidDataException or OperationCanceledException or IOException) { }
            Check(store.List().Count == before && !Directory.GetDirectories(root, ".merge-*").Any(), "failure leaves no published or temporary lesson");
        }
        await Reject([ids[0], ids[0]], new FakeDecoder(), CancellationToken.None);
        await Reject([ids[0]], new FakeDecoder(), CancellationToken.None);
        await Reject([..ids, pair], new FakeDecoder(), CancellationToken.None);
        await Reject(ids, new FakeDecoder(fail: true), CancellationToken.None);
        using var cancellation = new CancellationTokenSource();
        await Reject(ids, new FakeDecoder(cancel: cancellation), cancellation.Token);
        var live = store.Create("audio/webm;codecs=opus"); store.Append(live, new MediaPacket(0, 0, [1, 1], "fixture"));
        await Reject([live, ids[0]], new FakeDecoder(), CancellationToken.None); store.Finish(live, "user-stop");
        File.WriteAllText(Path.Combine(folder, "latest-export.json"), "{}"); store.RemoveRaw(merged);
        Check(!File.Exists(Path.Combine(folder, "audio.wav")), "raw retention cleanup removes merged WAV");
        await Reject([merged, ids[0]], new FakeDecoder(), CancellationToken.None);
        File.WriteAllBytes(Path.Combine(root, ids[0], "00000000.chunk"), [0]);
        await Reject(ids.Take(2).ToArray(), new FakeDecoder(), CancellationToken.None);
    }
    sealed class FakeDecoder(bool fail = false, CancellationTokenSource? cancel = null) : ILocalSpeechRuntime
    {
        public string Version => "fixture";
        public Task EnsureReady(IProgress<string>? progress, CancellationToken token) => throw new Exception("Network setup is forbidden in merge tests");
        public Task TranscribeChunk(string input, string output, string language, string log, CancellationToken token) => throw new Exception("Transcription is forbidden in merge tests");
        public Task<DecodedSpeechAudio> Decode(string input, string log, CancellationToken token) {
            var bytes = Enumerable.Repeat(File.ReadAllBytes(input)[0], 32000).ToArray();
            var audio = new MemoryStream(bytes);
            return Task.FromResult(new DecodedSpeechAudio(audio, () => {
                cancel?.Cancel(); token.ThrowIfCancellationRequested();
                if (fail) throw new InvalidDataException("Synthetic decoder failure");
                return Task.CompletedTask;
            }, () => { audio.Dispose(); return ValueTask.CompletedTask; }));
        }
    }
}
