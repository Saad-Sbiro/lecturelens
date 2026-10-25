using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using LectureLens.Core;
using LectureLens.Desktop;
using LectureLens.Storage;

internal static class Program
{
    static void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
    static void Wait(Func<bool> ready)
    {
        var frame = new DispatcherFrame();
        var deadline = Stopwatch.StartNew();
        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(20) };
        timer.Tick += (_, _) => { if (ready() || deadline.Elapsed.TotalSeconds > 8) { timer.Stop(); frame.Continue = false; } };
        timer.Start(); Dispatcher.PushFrame(frame);
        Check(ready(), "Native UI check timed out.");
    }

    [STAThread]
    static void Main()
    {
        var data = Path.Combine(Path.GetTempPath(), "lecturelens-desktop-ui-" + Guid.NewGuid().ToString("N"));
        Environment.SetEnvironmentVariable("LECTURELENS_DATA_ROOT", data);
        var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        Window? host = null;
        try
        {
            using var strings = JsonDocument.Parse(File.ReadAllText("config/native-ui.en.json"));
            foreach (var item in strings.RootElement.EnumerateObject()) app.Resources[item.Name] = item.Value.GetString();
            AppearanceSettings.Apply(app, new("dark", AppearanceSettings.DefaultFont));
            using var source = File.OpenRead("src/native/Desktop/MainWindow.xaml");
            var root = (Grid)XamlReader.Load(source);
            using var store = new LocalStore(LocalStore.DefaultRoot);
            var id = store.Create("audio/webm;codecs=opus");
            store.Append(id, new MediaPacket(0, 0, [1, 2, 3, 4], "synthetic-ui-fixture"));
            store.Finish(id, "fixture-end");
            store.Rename(id, "Original fixture");
            host = new Window { Content = root, Width = 1280, Height = 940, Left = -10000, Top = -10000, ShowInTaskbar = false, ShowActivated = false };
            var grid = (DataGrid)root.FindName("Sessions");
            new SessionPanel(root).Attach();
            WorkspaceMotion.Attach(root);
            host.Show();
            Wait(() => grid.Items.Count == 1);
            var rename = (Button)root.FindName("RenameLesson");
            grid.SelectedItem = null;
            rename.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Check(((TextBlock)root.FindName("SessionMessage")).Text == (string)app.Resources["chooseSession"], "Rename without a selected lesson must explain the next step.");
            grid.SelectedIndex = 0;
            Exception? dialogError = null;
            var safety = new DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
            safety.Tick += (_, _) => { safety.Stop(); foreach (var window in app.Windows.Cast<Window>().Where(w => w != host).ToArray()) window.Close(); };
            safety.Start();
            app.Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() =>
            {
                try
                {
                    var dialog = app.Windows.Cast<Window>().Single(w => w != host);
                    var panel = (StackPanel)dialog.Content;
                    var input = panel.Children.OfType<TextBox>().Single();
                    var save = panel.Children.OfType<Button>().Single();
                    Check(input.Text == "Original fixture", "Rename must open for the selected lesson.");
                    input.Text = "  "; save.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                    Check(dialog.IsVisible && store.List().Single().Title == "Original fixture", "Invalid names must preserve the dialog and original metadata.");
                    input.Text = "Updated / fixture"; save.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                }
                catch (Exception error) { dialogError = error; foreach (var window in app.Windows.Cast<Window>().Where(w => w != host).ToArray()) window.Close(); }
            }));
            rename.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            safety.Stop();
            if (dialogError != null) throw dialogError;
            Wait(() => grid.SelectedItem is SessionInfo lesson && lesson.Title == "Updated / fixture"
                && ((TextBlock)root.FindName("SessionMessage")).Text == (string)app.Resources["lessonRenamed"]);
            Check(store.List().Single().Title == "Updated / fixture" && Directory.Exists(Path.Combine(store.Root, id)), "Renaming must persist without moving the recording.");
            Check(((TextBlock)root.FindName("SessionMessage")).Text == (string)app.Resources["lessonRenamed"], "Successful rename must confirm completion.");
            var companion = (Button)root.FindName("CompanionButton");
            Check(companion.ToolTip == null, "Lumi must have no hover tooltip.");
            var caption = ((Grid)companion.Content).Children.OfType<StackPanel>().Single().Children.OfType<TextBlock>().ToArray();
            Check(caption.Length == 1 && caption[0].Text == "Lumi", "Lumi's caption must contain only the name.");
            Directory.CreateDirectory("artifacts/lumi-polish");
            foreach (var theme in new[] { "dark", "light" })
            {
                AppearanceSettings.Apply(app, new(theme, AppearanceSettings.DefaultFont)); host.UpdateLayout();
                var art = (ContentControl)root.FindName("CompanionArt");
                var drawing = (FrameworkElement)art.Content;
                var bitmap = new RenderTargetBitmap(480, 352, 192, 192, PixelFormats.Pbgra32); bitmap.Render(drawing);
                var png = new PngBitmapEncoder(); png.Frames.Add(BitmapFrame.Create(bitmap));
                using var output = File.Create($"artifacts/lumi-polish/{theme}.png"); png.Save(output);
            }
            Console.WriteLine("PASS: production Rename button opens dialog, rejects blank names, saves metadata, retains selection/recording ID; Lumi has no tooltip and a name-only caption.");
        }
        finally { host?.Close(); if (Directory.Exists(data)) Directory.Delete(data, true); app.Shutdown(); }
    }
}
