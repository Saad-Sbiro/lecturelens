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
        AppearanceSettings.Apply(app,new AppearanceSettings.Choice("dark",AppearanceSettings.DefaultFont));
        var xaml = File.ReadAllText("src/native/Desktop/MainWindow.xaml").Replace("LectureLens;component", "LectureLens.MergeChecks;component");
        var view = (FrameworkElement)XamlReader.Parse(xaml);
        var owner = new Window { Content = view, Width = 1280, Height = 900 }; owner.Show();
        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(200) };
        Exception? failure = null;
        var mergeFrame = new DispatcherFrame();
        timer.Tick += (_, _) => {
            timer.Stop(); var overlay = ((Grid)view).Children.OfType<Grid>().Single(g => g.Name == "DialogOverlay");
            var dialog = owner;
            try {
                if (app.Windows.Count != 1) throw new Exception("Merge opened another window");
                var layout = (DockPanel)((Border)overlay.Children[0]).Child;
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
            finally {
                overlay.RaiseEvent(new System.Windows.Input.KeyEventArgs(System.Windows.Input.Keyboard.PrimaryDevice, PresentationSource.FromVisual(owner), 0, System.Windows.Input.Key.Escape) { RoutedEvent = System.Windows.Input.Keyboard.PreviewKeyDownEvent });
                mergeFrame.Continue = false;
            }
        };
        timer.Start();
        var lessons = new[] { "Part A", "Part B", "Part C" }.Select(title => new SessionInfo(Guid.NewGuid().ToString(), "2026-10-23T10:00:00Z", "finished", 1, 32000, [], Title: title)).ToArray();
        var mergeTask = MergeWindow.Show(view, lessons);
        Dispatcher.PushFrame(mergeFrame);
        if (!mergeTask.IsCompleted || mergeTask.Result != null) throw new Exception("Merge dismiss failed");
        Notifications.Attach(view);
        var toast = (Border)view.FindName("NotificationToast");
        if (toast.Visibility != Visibility.Collapsed) throw new Exception("Idle notification visible");
        ((TextBlock)view.FindName("SessionMessage")).Text = "Fixture saved";
        if (toast.Visibility != Visibility.Visible) throw new Exception("Notification did not appear");
        ((Button)view.FindName("DismissNotification")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        if (toast.Visibility != Visibility.Collapsed) throw new Exception("Notification dismiss failed");
        Notifications.PdfReady(view,Path.GetFullPath("artifacts/fixture.pdf"));
        var pdfOverlay = ((Grid)view).Children.OfType<Grid>().Single(g => g.Name == "DialogOverlay");
        var pdfPanel = (StackPanel)((Border)pdfOverlay.Children[0]).Child;
        var pdfActions = pdfPanel.Children.OfType<WrapPanel>().Single().Children.OfType<Button>().ToArray();
        if (app.Windows.Count != 1 || !pdfActions.Any(b => Equals(b.Content,app.Resources["openInBrowser"])) || !pdfActions.Any(b => Equals(b.Content,app.Resources["openFileLocation"]))) throw new Exception("PDF choices missing");
        pdfActions.Single(b => Equals(b.Content,app.Resources["close"])).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        if (((Grid)view).Children.OfType<Grid>().Any(g => g.Name == "DialogOverlay")) throw new Exception("PDF dismiss failed");
        Console.WriteLine("PASS notifications and PDF completion choices stay inside the app");
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
        var previousRoot = Environment.GetEnvironmentVariable("LECTURELENS_DATA_ROOT");
        Environment.SetEnvironmentVariable("LECTURELENS_DATA_ROOT", Path.GetFullPath(Path.Combine("artifacts", "startup-check", Guid.NewGuid().ToString())));
        using (var store = new LectureLens.Storage.LocalStore(LectureLens.Storage.LocalStore.DefaultRoot)) {
            var id = store.Create("audio/wav");
            store.Finish(id, "fixture");
            if (!view.IsLoaded) throw new Exception("Startup regression requires an already loaded view");
            new SessionPanel(view).Attach();
            var startupFrame = new DispatcherFrame();
            var startupTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(50) };
            int startupTicks = 0;
            startupTimer.Tick += (_, _) => {
                var grid = (DataGrid)view.FindName("Sessions");
                if (grid.Items.OfType<SessionInfo>().Any(s => s.Id == id)) {
                    Console.WriteLine("PASS lessons load automatically when attached after Loaded without Refresh");
                    startupTimer.Stop(); startupFrame.Continue = false;
                } else if (++startupTicks >= 100) {
                    failure = new Exception("Lessons missing on startup");
                    startupTimer.Stop(); startupFrame.Continue = false;
                }
            };
            startupTimer.Start(); Dispatcher.PushFrame(startupFrame);
        }
        var scroll = new ScrollViewer {
            Width = 360, Height = 240, HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center, VerticalScrollBarVisibility = ScrollBarVisibility.Visible,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Visible,
            Content = new Border { Width = 900, Height = 900, Background = (Brush)app.Resources["PanelBackground"],
                Child = new TextBlock { Text = "Scroll preview", Margin = new Thickness(20) } }
        };
        Grid.SetColumnSpan(scroll, 4); Grid.SetRowSpan(scroll, 3); ((Grid)view).Children.Add(scroll);
        foreach (var theme in new[] { "dark", "light" }) {
            AppearanceSettings.Apply(app, new AppearanceSettings.Choice(theme, AppearanceSettings.DefaultFont));
            owner.UpdateLayout(); scroll.ScrollToVerticalOffset(100); scroll.ScrollToHorizontalOffset(100); owner.UpdateLayout();
            foreach (var name in new[] { "PART_VerticalScrollBar", "PART_HorizontalScrollBar" }) {
                var bar = (System.Windows.Controls.Primitives.ScrollBar)scroll.Template.FindName(name, scroll);
                if (bar.Template.FindName("Grip", bar) == null) throw new Exception("Custom scrollbar template missing: " + name);
            }
            if (scroll.VerticalOffset != 100 || scroll.HorizontalOffset != 100) throw new Exception("Scrollbar offsets failed");
            var visual = new DrawingVisual();
            using (var drawing = visual.RenderOpen()) {
                drawing.DrawRectangle((Brush)app.Resources["AppBackground"], null, new Rect(0, 0, 360, 240));
                drawing.DrawRectangle(new VisualBrush(scroll), null, new Rect(0, 0, 360, 240));
            }
            var shot = new RenderTargetBitmap(360, 240, 96, 96, PixelFormats.Pbgra32); shot.Render(visual);
            var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(shot));
            using var output = File.Create("artifacts/scrollbars-" + theme + ".png"); encoder.Save(output);
        }
        Console.WriteLine("PASS themed horizontal and vertical scrollbars render and scroll");
        owner.Close(); app.Shutdown();
        Environment.SetEnvironmentVariable("LECTURELENS_DATA_ROOT", previousRoot);
        if (failure != null) throw failure;
    }
}
