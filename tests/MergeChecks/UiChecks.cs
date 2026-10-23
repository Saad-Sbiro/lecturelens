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

internal static class UiChecks
{
    internal static void Run()
    {
        var app = new Application();
        using var strings = JsonDocument.Parse(File.ReadAllText("config/native-ui.en.json"));
        foreach (var item in strings.RootElement.EnumerateObject()) app.Resources[item.Name] = item.Value.GetString();
        var xaml = File.ReadAllText("src/native/Desktop/MainWindow.xaml").Replace("LectureLens;component", "LectureLens.MergeChecks;component");
        var view = (FrameworkElement)XamlReader.Parse(xaml);
        var owner = new Window { Content = view, Width = 1280, Height = 900 }; owner.Show();
        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(200) };
        Exception? failure = null;
        timer.Tick += (_, _) => {
            timer.Stop(); var dialog = app.Windows.Cast<Window>().Single(w => w != owner);
            try {
                var layout = (DockPanel)dialog.Content;
                var panel = (StackPanel)layout.Children.OfType<ScrollViewer>().Single().Content;
                var footer = layout.Children.OfType<StackPanel>().Single();
                var buttons = panel.Children.OfType<Button>().Concat(panel.Children.OfType<Panel>().Concat(footer.Children.OfType<Panel>()).SelectMany(p => p.Children.OfType<Button>())).ToArray();
                Button Find(string key) => buttons.Single(b => Equals(b.Content, app.Resources[key]));
                var available = panel.Children.OfType<ComboBox>().Single(); var list = panel.Children.OfType<ListBox>().Single();
                for (int i = 0; i < 3; i++) { available.SelectedIndex = i; Find("mergeAdd").RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); }
                if (Find("mergeAdd").IsEnabled) throw new Exception("Fourth input allowed");
                list.SelectedIndex = 2; Find("mergeUp").RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); Find("mergeUp").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                if (!list.Items[0].ToString()!.Contains("Part C") || Find("mergeUp").IsEnabled) throw new Exception("Order controls failed");
                if (!Find("mergeCreate").IsEnabled) throw new Exception("Merge action unavailable");
                dialog.UpdateLayout();
                var bitmap = new RenderTargetBitmap((int)dialog.ActualWidth, (int)dialog.ActualHeight, 96, 96, PixelFormats.Pbgra32); bitmap.Render(dialog);
                var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
                using (var file = File.Create("artifacts/merge-window.png")) encoder.Save(file);
                Find("mergeRemove").RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); Find("mergeRemove").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                if (Find("mergeCreate").IsEnabled) throw new Exception("One-input merge allowed");
                Console.WriteLine("PASS native merge selection, reorder, limits and screenshot");
            }
            catch (Exception e) { failure = e; }
            finally { dialog.Close(); }
        };
        timer.Start();
        var lessons = new[] { "Part A", "Part B", "Part C" }.Select(title => new SessionInfo(Guid.NewGuid().ToString(), "2026-10-23T10:00:00Z", "finished", 1, 32000, [], Title: title)).ToArray();
        MergeWindow.Show(view, lessons);
        var folder = Path.GetFullPath("artifacts/slide-review-check");
        Directory.CreateDirectory(Path.Combine(folder, "slides"));
        var jpeg = new JpegBitmapEncoder();
        jpeg.Frames.Add(BitmapFrame.Create(BitmapSource.Create(2, 2, 96, 96, PixelFormats.Bgr24, null, new byte[12], 6)));
        using var bytes = new MemoryStream(); jpeg.Save(bytes);
        var data = bytes.ToArray();
        File.WriteAllText(Path.Combine(folder, "slides", "00000.slide.json"), JsonSerializer.Serialize(new { slide = 0, atMs = 0, data = Convert.ToBase64String(data), sha256 = Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(data)) }));
        var reviewTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(100) };
        var frame = new DispatcherFrame();
        int ticks = 0;
        reviewTimer.Tick += (_, _) => {
            var page = ((Grid)view).Children.OfType<SlideReviewWindow>().Single();
            var layout = (DockPanel)page.Content;
            var cards = (WrapPanel)layout.Children.OfType<ScrollViewer>().Single().Content;
            if (cards.Children.Count == 1) {
                if (app.Windows.Count != 1) failure = new Exception("Slide review opened another window");
                layout.Children.OfType<Button>().Single(b => b.Name == "BackToLesson").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                if (((Grid)view).Children.OfType<SlideReviewWindow>().Any()) failure = new Exception("Back navigation failed");
                Console.WriteLine("PASS slide review loads in the same window and returns to lesson");
                reviewTimer.Stop(); frame.Continue = false;
            } else if (++ticks >= 50) {
                failure = new Exception("Slide review failed to load within five seconds");
                reviewTimer.Stop(); frame.Continue = false;
            }
        };
        reviewTimer.Start();
        SlideReviewWindow.ShowFor(view, folder);
        Dispatcher.PushFrame(frame);
        owner.Close(); app.Shutdown();
        if (failure != null) throw failure;
    }
}
