using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Interop;
using System.Windows.Input;
using System.Windows.Shell;
using System.Windows.Threading;

namespace LectureLens.Desktop;

public static class Program
{
    [STAThread]
    public static int Main(string[] args)
    {
        var app = new Application();
        var smoke = args.Contains("--smoke");
        if (smoke) RenderOptions.ProcessRenderMode = RenderMode.SoftwareOnly;
        var source = Environment.GetEnvironmentVariable("LECTURELENS_XAML") ?? Path.Combine(AppContext.BaseDirectory, "MainWindow.xaml");
        using var strings = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "ui-strings.json")));
        foreach (var item in strings.RootElement.EnumerateObject()) app.Resources[item.Name] = item.Value.GetString();
        var appearance = AppearanceSettings.Load();
        if (smoke && args.FirstOrDefault(value => value.StartsWith("--theme=", StringComparison.Ordinal)) is string themeArgument)
            appearance = appearance with { Theme = themeArgument[8..] };
        AppearanceSettings.Apply(app, appearance);
        var window = new Window { Title = "LectureLens", Width = 1280, Height = 900, MinWidth = 1120, MinHeight = 600, WindowState = WindowState.Maximized, WindowStyle = WindowStyle.None };
        WindowChrome.SetWindowChrome(window, new WindowChrome { CaptionHeight = 0, ResizeBorderThickness = new Thickness(7), GlassFrameThickness = new Thickness(0), CornerRadius = new CornerRadius(0), UseAeroCaptionButtons = false });
        window.SetResourceReference(Window.BackgroundProperty, "AppBackground");
        WindowBounds.Attach(window);
        window.Icon = TaskbarIcon.Create();

        int reloads = 0;
        bool fullScreen = false;
        void ToggleMaximize() => window.WindowState = window.WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
        void ToggleFullScreen()
        {
            if (window.Content is not Grid root || root.FindName("TitleBar") is not Border titleBar) return;
            fullScreen = !fullScreen;
            titleBar.Visibility = fullScreen ? Visibility.Collapsed : Visibility.Visible;
            root.RowDefinitions[0].Height = fullScreen ? new GridLength(0) : new GridLength(40);
            window.WindowState = WindowState.Maximized;
        }
        window.PreviewKeyDown += (_, eventArgs) => { if (eventArgs.Key == Key.F11) { ToggleFullScreen(); eventArgs.Handled = true; } };
        void Reload()
        {
            // Only developer-owned local XAML is loaded; capture will not depend on UI reloads.
            using var stream = File.OpenRead(source);
            var view = (FrameworkElement)XamlReader.Load(stream);
            var titleBar = (Border)view.FindName("TitleBar");
            titleBar.MouseLeftButtonDown += (_, eventArgs) => { if (eventArgs.ClickCount == 2) ToggleMaximize(); else window.DragMove(); };
            ((Button)view.FindName("MinimizeWindow")).Click += (_, _) => window.WindowState = WindowState.Minimized;
            ((Button)view.FindName("MaximizeWindow")).Click += (_, _) => ToggleMaximize();
            ((Button)view.FindName("CloseWindow")).Click += (_, _) => window.Close();
            if (fullScreen && view is Grid root) { titleBar.Visibility = Visibility.Collapsed; root.RowDefinitions[0].Height = new GridLength(0); }
            ((Button)view.FindName("OpenFolder")).Click += (_, _) =>
            {
                var folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "LectureLens", "recordings");
                Directory.CreateDirectory(folder);
                Process.Start(new ProcessStartInfo(folder) { UseShellExecute = true });
            };
            window.Content = view;
            new SessionPanel(view).Attach();
            new NotesStudio(view).Attach();
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
        catch (Exception e) { if (smoke) { Directory.CreateDirectory("artifacts"); File.WriteAllText("artifacts/native-smoke-error.txt", e.ToString()); } else MessageBox.Show(e.Message, "LectureLens"); return 1; }
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

                ToggleFullScreen();
                if (!fullScreen || ((Grid)window.Content).RowDefinitions[0].Height.Value != 0) throw new Exception("F11 full-screen entry failed");
                ToggleFullScreen();
                if (fullScreen || ((Grid)window.Content).RowDefinitions[0].Height.Value != 40) throw new Exception("F11 full-screen exit failed");
                var shell = (Grid)window.Content;
                ((Button)shell.FindName("KeySetup")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                var settingsOverlay = shell.Children.OfType<Grid>().Single(g => g.Name == "DialogOverlay");
                var settingsPanel = (StackPanel)((Border)settingsOverlay.Children[0]).Child;
                if (settingsPanel.Children.OfType<PasswordBox>().Any() || settingsPanel.Children.OfType<ComboBox>().Count() != 2)
                    throw new Exception("Settings must contain appearance controls without API keys");
                settingsPanel.Children.OfType<WrapPanel>().Single().Children.OfType<Button>()
                    .Single(b => Equals(b.Content, app.Resources["cancelGeneration"])).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                if (shell.Children.OfType<Grid>().Any(g => g.Name == "DialogOverlay")) throw new Exception("Settings did not close");
                window.UpdateLayout();
                var bitmap = new RenderTargetBitmap((int)window.ActualWidth, (int)window.ActualHeight, 96, 96, PixelFormats.Pbgra32);
                bitmap.Render(window);
                var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
                Directory.CreateDirectory("artifacts");
                using (var file = File.Create("artifacts/native-shell.png")) encoder.Save(file);
                File.WriteAllText("artifacts/native-smoke.json", JsonSerializer.Serialize(new { desktop = "WPF", webView = false, loaded = true, liveReload = watcher.EnableRaisingEvents, reloads, fullScreen = true, capture = "not-run" }));
                app.Shutdown(0);
            }
            catch (Exception e) { Directory.CreateDirectory("artifacts"); File.WriteAllText("artifacts/native-smoke-error.txt", e.ToString()); app.Shutdown(1); }
        };
        return app.Run(window);
    }
}
