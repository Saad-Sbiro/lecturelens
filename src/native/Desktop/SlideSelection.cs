using System.IO;
using System.Text.Json;

namespace LectureLens.Desktop;

internal static class SlideSelection
{
    private const string FileName = "excluded.json";

    internal static HashSet<int> Load(string sessionFolder)
    {
        var path = Path.Combine(sessionFolder, "slides", FileName);
        if (!File.Exists(path)) return [];
        try
        {
            var values = JsonSerializer.Deserialize<int[]>(File.ReadAllText(path)) ?? [];
            if (values.Any(value => value is < 0 or >= 720)) throw new InvalidDataException("Invalid hidden slide identifier.");
            return values.ToHashSet();
        }
        catch (JsonException error)
        {
            throw new InvalidDataException("The hidden-slide list is damaged. Original slides were not changed.", error);
        }
    }

    internal static void Save(string sessionFolder, IEnumerable<int> excluded)
    {
        var folder = Path.Combine(sessionFolder, "slides");
        Directory.CreateDirectory(folder);
        var path = Path.Combine(folder, FileName);
        var partial = path + ".partial";
        File.WriteAllText(partial, JsonSerializer.Serialize(excluded.Distinct().Order().ToArray()));
        File.Move(partial, path, true);
    }
}
