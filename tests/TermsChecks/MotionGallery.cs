using System.IO;
using System.Reflection;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using LectureLens.Desktop;

internal static class MotionGallery
{
    internal static void Run(Application app)
    {
        var assembly = Assembly.LoadFrom(Path.GetFullPath("artifacts/alive-desktop/LectureLens.dll"));
        var appearance = assembly.GetType("LectureLens.Desktop.AppearanceSettings")!;
        var choice = Activator.CreateInstance(appearance.GetNestedType("Choice", BindingFlags.NonPublic)!, "dark", "Segoe UI")!;
        appearance.GetMethod("Apply", BindingFlags.Static | BindingFlags.NonPublic)!.Invoke(null, [app, choice]);
        using var strings = JsonDocument.Parse(File.ReadAllText("config/native-ui.en.json"));
        foreach (var item in strings.RootElement.EnumerateObject()) app.Resources[item.Name] = item.Value.GetString();
        using var source = File.OpenRead("src/native/Desktop/MainWindow.xaml");
        var view = (Grid)XamlReader.Load(source);
        ((TextBlock)view.FindName("LessonTitle")).Text = "How learning becomes understanding";
        ((TextBox)view.FindName("TranscriptPreview")).Text = "MOTION PREVIEW · SYNTHETIC LESSON\n\nLearning happens when we connect a new idea to something we already know.\n\nTry explaining the concept in your own words. Then return to the original example and see what you missed.\n\nKeep the explanation simple. Understanding grows through practice.";
        ((TextBlock)view.FindName("TranscriptStatus")).Text = "Animation demo · no recording or AI request";
        foreach (var name in new[] { "TeacherLanguage", "PdfLanguage", "Detail" }) {
            var combo = (ComboBox)view.FindName(name); combo.ItemsSource = new[] { "Sample option", "Another option" }; combo.SelectedIndex = 0;
        }
        ((Button)view.FindName("CancelGeneration")).IsEnabled = false;
        var sessions = (DataGrid)view.FindName("Sessions");
        sessions.ItemsSource = new[] { new { Title = "Learning & memory", DisplayDate = "Sample lesson", State = "ready" }, new { Title = "A clearer explanation", DisplayDate = "Sample lesson", State = "ready" } };
        sessions.SelectedIndex = 0;
        Motion.Initialize();
        var host = new Window { Content = view, Width = 1200, Height = 760, Left = -10000, Top = -10000, ShowActivated = false, ShowInTaskbar = false, WindowStyle = WindowStyle.None };
        host.Show(); host.UpdateLayout(); ControlMotion.AttachTree(view);
        var folder = "artifacts/motion-gallery"; Directory.CreateDirectory(folder); var frame = 0;
        void Frames(int count) {
            for (var i = 0; i < count; i++) {
                var loop = new DispatcherFrame(); var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(45) };
                timer.Tick += (_, _) => { timer.Stop(); loop.Continue = false; }; timer.Start(); Dispatcher.PushFrame(loop);
                view.UpdateLayout(); var bitmap = new RenderTargetBitmap(1200, 760, 96, 96, PixelFormats.Pbgra32); bitmap.Render(view);
                var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
                using var file = File.Create(Path.Combine(folder, $"{frame++:D3}.png")); encoder.Save(file);
            }
        }
        try {
            Frames(5);
            var button = (Button)view.FindName("GeneratePdf");
            button.RaiseEvent(new MouseEventArgs(Mouse.PrimaryDevice, 0) { RoutedEvent = Mouse.MouseEnterEvent }); Frames(5);
            button.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice, 0, MouseButton.Left) { RoutedEvent = UIElement.PreviewMouseLeftButtonDownEvent }); Frames(3);
            button.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice, 0, MouseButton.Left) { RoutedEvent = UIElement.PreviewMouseLeftButtonUpEvent }); Frames(5);
            sessions.SelectedIndex = 1; SpatialMotion.Reveal((FrameworkElement)view.FindName("TranscriptPreview")); Frames(8);
            var cancel = (Button)view.FindName("CancelGeneration");
            cancel.IsEnabled = true; Frames(12); cancel.IsEnabled = false; Frames(3);
            var panel = new StackPanel();
            panel.Children.Add(new TextBlock { Text = "A little more life.", FontSize = 28, Margin = new Thickness(0, 0, 0, 16) });
            panel.Children.Add(new TextBlock { Text = "Same LectureLens. Responsive controls, fluid transitions.\nThis is a motion preview with synthetic lesson data.", Margin = new Thickness(0, 0, 0, 20) });
            panel.Children.Add(new Button { Content = "Continue", Style = (Style)view.FindResource("Primary") });
            var dialog = new InAppDialog(view, panel); Frames(16); dialog.Close(); Frames(5);
            Console.WriteLine($"Rendered {frame} native motion frames to {folder}.");
        } finally { host.Close(); }
    }
}
