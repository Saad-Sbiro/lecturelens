using System.Diagnostics;
using System.IO;

namespace LectureLens.Desktop;

internal static class DesktopCapture
{
    public static void Open()
    {
        var runtime = Environment.GetEnvironmentVariable("LECTURELENS_CAPTURE_RUNTIME") ?? Path.Combine(AppContext.BaseDirectory, "capture-runtime", "electron.exe");
        var entry = Environment.GetEnvironmentVariable("LECTURELENS_CAPTURE_ENTRY") ?? Path.Combine(AppContext.BaseDirectory, "desktop-capture", "main.cjs");
        if (!File.Exists(runtime) || !File.Exists(entry)) throw new FileNotFoundException("Desktop capture is missing. Run npm run dev or rebuild the native package.");
        var start = new ProcessStartInfo(runtime) { UseShellExecute = false, CreateNoWindow = true };
        start.Environment.Remove("ELECTRON_RUN_AS_NODE");
        start.ArgumentList.Add(entry);
        Process.Start(start);
    }
}
