using System.IO;
using LectureLens.Core;

namespace LectureLens.Desktop.Adapters;

internal static class SlideTimeline
{
 internal static async Task<NoteSource[]> Align(string folder, IReadOnlyList<NoteSource> sources, CancellationToken cancel)
 {
  var directory = Path.Combine(folder, "slides");
  if (!Directory.Exists(directory)) return sources.Select(s => s with { SlideId = null }).ToArray();
  var timeline = new List<(int Id, double Seconds)>();
  foreach (var path in Directory.GetFiles(directory, "*.slide.json").Order(StringComparer.Ordinal)) {
   var slide = await BrowserPdf.ReadSlide(path, cancel);
   timeline.Add((slide.Number, RecordingMerge.AudioTime(folder, slide.AtMs) / 1000));
  }
  if (timeline.Select(s => s.Id).Distinct().Count() != timeline.Count) throw new InvalidDataException("Duplicate slide identifier.");
  timeline = timeline.OrderBy(s => s.Seconds).ThenBy(s => s.Id).ToList();
  return sources.Select(source => {
   // Imported text without a known clock cannot honestly be assigned to a slide.
   if (source.TimestampBasis != "decoded-audio" || source.Start is not double start || !double.IsFinite(start)) return source with { SlideId = null };
   var end = source.End ?? start;
   var best = -1; double overlap = 0;
   for (int i = 0; i < timeline.Count; i++) {
    var until = i + 1 < timeline.Count ? timeline[i + 1].Seconds : double.PositiveInfinity;
    var duration = Math.Max(0, Math.Min(end, until) - Math.Max(start, timeline[i].Seconds));
    if (duration > overlap || end == start && start >= timeline[i].Seconds && start < until) { best = i; overlap = duration; }
   }
   return source with { SlideId = best < 0 ? null : timeline[best].Id };
  }).ToArray();
 }
 internal static List<NoteSource[]> Parts(IReadOnlyList<NoteSource> sources)
 {
  var result = new List<NoteSource[]>(); var batch = new List<NoteSource>(); int size = 0;
  foreach (var source in sources) {
   if (batch.Count > 0 && (size + source.Text.Length > 10000 || source.SlideId != batch[0].SlideId)) { result.Add(batch.ToArray()); batch.Clear(); size = 0; }
   batch.Add(source); size += source.Text.Length;
  }
  if (batch.Count > 0) result.Add(batch.ToArray());
  return result;
 }
}
