using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Media;
using LectureLens.Desktop.Adapters;

namespace LectureLens.Desktop;

internal static class AppearanceSettings
{
    internal sealed record Choice(string Theme, string Font);
    internal static readonly string[] Themes = ["dark", "light"];
    internal static readonly string[] Fonts = InstalledFonts();
    internal static string DefaultFont => Fonts[0];
    static string PathName => Path.Combine(KeyVault.Folder, "appearance.json");

    internal static Choice Load()
    {
        if (File.Exists(PathName)) try
        {
            var saved = JsonSerializer.Deserialize<Choice>(File.ReadAllText(PathName));
            if (saved is not null && Themes.Contains(saved.Theme) && Fonts.Contains(saved.Font)) return saved;
        }
        catch (JsonException) { }
        return new("dark", DefaultFont);
    }

    internal static void Save(Choice choice)
    {
        if (!Themes.Contains(choice.Theme) || !Fonts.Contains(choice.Font)) throw new InvalidDataException("Invalid appearance setting.");
        Directory.CreateDirectory(KeyVault.Folder);
        File.WriteAllText(PathName + ".partial", JsonSerializer.Serialize(choice));
        File.Move(PathName + ".partial", PathName, true);
    }

    internal static void Apply(Application app, Choice choice)
    {
        var light = choice.Theme == "light";
        Set(app, "AppBackground", light ? "#F5F2EB" : "#191B21");
        Set(app, "AppForeground", light ? "#28231B" : "#EDF0F7");
        Set(app, "RailBackground", light ? "#E8E1D5" : "#111318");
        Set(app, "TitleBarBackground", light ? "#F5F2EB" : "#191B21");
        Set(app, "PanelBackground", light ? "#FBF9F5" : "#20232B");
        Set(app, "PanelAltBackground", light ? "#F0ECE4" : "#23262F");
        Set(app, "SurfaceBackground", light ? "#FFFFFF" : "#30333E");
        Set(app, "DeepSurfaceBackground", light ? "#F6F2EA" : "#191B23");
        Set(app, "AppBorder", light ? "#D6CDBE" : "#404451");
        Set(app, "AppMuted", light ? "#6D655B" : "#A2A9B9");
        Set(app, "HoverBackground", light ? "#EFE6D8" : "#2C303B");
        Set(app, "SelectedBackground", light ? "#F4E4C5" : "#3A342A");
        Set(app, "Accent", light ? "#C77B00" : "#F7B545");
        Set(app, "AccentSoft", light ? "#FFF0D0" : "#383326");
        Set(app, "AccentText", light ? "#6B4100" : "#FFD794");
        Set(app, "AccentForeground", light ? "#FFFFFF" : "#271B0B");
        Set(app, "Danger", light ? "#A83B3B" : "#DB9D9B");
        Set(app, "DangerSurface", light ? "#B4232D" : "#B83B42");
        app.Resources["AppFont"] = new FontFamily(choice.Font);
        app.Resources["IconFont"] = new FontFamily(HasFont("Segoe Fluent Icons") ? "Segoe Fluent Icons" : "Segoe MDL2 Assets");
    }

    static void Set(Application app, string key, string color) => app.Resources[key] = new SolidColorBrush((Color)ColorConverter.ConvertFromString(color));
    static bool HasFont(string name) => System.Windows.Media.Fonts.SystemFontFamilies.Any(font => font.Source.Equals(name, StringComparison.OrdinalIgnoreCase));
    static string[] InstalledFonts()
    {
        var preferred = new[] { "Segoe UI Variable Text", "Segoe UI", "Aptos", "Calibri", "Arial" };
        var installed = preferred.Where(HasFont).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        return installed.Length > 0 ? installed : ["Segoe UI"];
    }
}
