using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace LectureLens.Desktop;

public static class Program
{
    [STAThread]
    public static int Main(string[] args)
    {
        var app = new Application();
        var smoke = args.Contains("--smoke");
        var source = Environment.GetEnvironmentVariable("LECTURELENS_XAML") ?? Path.Combine(AppContext.BaseDirectory, "MainWindow.xaml");
        using var strings = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "ui-strings.json")));
        foreach (var item in strings.RootElement.EnumerateObject()) app.Resources[item.Name] = item.Value.GetString();
        var window = new Window { Title = "LectureLens - Native preview", Width = 1000, Height = 740, MinWidth = 740, MinHeight = 600, Background = new SolidColorBrush(Color.FromRgb(17, 24, 39)) };
        int reloads = 0;
        void Reload()
        {
            // Only developer-owned local XAML is loaded; capture will not depend on UI reloads.
            using var stream = File.OpenRead(source);
            var view = (FrameworkElement)XamlReader.Load(stream);
            ((Button)view.FindName("OpenFolder")).Click += (_, _) =>
            {
                var folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "LectureLens", "recordings");
                Directory.CreateDirectory(folder);
                Process.Start(new ProcessStartInfo(folder) { UseShellExecute = true });
            };
            window.Content = view;
            new SessionPanel(view).Attach();
            reloads++;
        }
        var debounce = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(200) };
        debounce.Tick += (_, _) =>
        {
            debounce.Stop();
            try { Reload(); }
            catch (Exception e) { Debug.WriteLine(e); /* Preserve the last valid UI during an incomplete edit. */ }
        };
        using var watcher = new FileSystemWatcher(Path.GetDirectoryName(source)!, Path.GetFileName(source));
        watcher.NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.Size;
        void Changed() => app.Dispatcher.BeginInvoke(() => { debounce.Stop(); debounce.Start(); });
        watcher.Changed += (_, _) => Changed();
        watcher.Created += (_, _) => Changed();
        watcher.Renamed += (_, _) => Changed();
        watcher.EnableRaisingEvents = Environment.GetEnvironmentVariable("LECTURELENS_XAML") != null;
        try { Reload(); }
        catch (Exception e) { MessageBox.Show(e.Message, "LectureLens"); return 1; }
        if (smoke) window.ContentRendered += async (_, _) =>
        {
            try
            {
                if (watcher.EnableRaisingEvents)
                {
                    var before = reloads;
                    var original = await File.ReadAllTextAsync(source);
                    await File.WriteAllTextAsync(source, original.Replace("Tag=\"preview\"", "Tag=\"reload-verified\""));
                    var deadline = DateTime.UtcNow.AddSeconds(8);
                    while (reloads == before && DateTime.UtcNow < deadline) await Task.Delay(100);
                    if (((FrameworkElement)window.Content).Tag?.ToString() != "reload-verified") throw new InvalidOperationException("XAML live reload failed");
                }
                window.UpdateLayout();
                var bitmap = new RenderTargetBitmap((int)window.ActualWidth, (int)window.ActualHeight, 96, 96, PixelFormats.Pbgra32);
                bitmap.Render(window);
                var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
                Directory.CreateDirectory("artifacts");
                using (var file = File.Create("artifacts/native-shell.png")) encoder.Save(file);
                File.WriteAllText("artifacts/native-smoke.json", JsonSerializer.Serialize(new { desktop = "WPF", webView = false, loaded = true, liveReload = watcher.EnableRaisingEvents, reloads, capture = "not-run" }));
                app.Shutdown(0);
            }
            catch (Exception e) { Directory.CreateDirectory("artifacts"); File.WriteAllText("artifacts/native-smoke-error.txt", e.ToString()); app.Shutdown(1); }
        };
        return app.Run(window);
    }
}
