using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using System.Windows.Input;
using System.Windows.Media;
using LectureLens.Desktop;

internal static class Program
{
    [STAThread]
    static int Main()
    {
        var app = new Application();
        var root = new Grid();
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(40) });
        root.RowDefinitions.Add(new RowDefinition());
        root.RowDefinitions.Add(new RowDefinition());
        root.Resources["desktopCaptureTitle"] = "Record a desktop lesson";
        root.Resources["backToLesson"] = "Back to lesson";
        root.Resources["desktopCaptureHint"] = "Choose the app window for your lesson.";
        root.Resources["desktopCaptureOpening"] = "Opening desktop capture";
        root.Resources["AppBackground"] = new SolidColorBrush(Color.FromRgb(25, 27, 33));
        root.Resources["AppForeground"] = Brushes.White;
        root.Resources["AppMuted"] = Brushes.Gray;
        root.Resources["AppFont"] = new FontFamily("Segoe UI");
        root.Resources["PanelBackground"] = System.Windows.Media.Brushes.Black;
        root.Resources["Quiet"] = new Style(typeof(Button));
        var page = new Border { Focusable = true }; Grid.SetRow(page, 1); root.Children.Add(page);
        var hidden = new Border { Visibility = Visibility.Hidden }; Grid.SetRow(hidden, 2); root.Children.Add(hidden);
        var title = new Border(); root.Children.Add(title);
        var window = new Window { Content = root, Width = 1200, Height = 850, Left = -12000, Top = -12000, ShowInTaskbar = false, WindowStyle = WindowStyle.None };
        var result = 1;
        window.ContentRendered += async (_, _) => {
            try
            {
                page.Focus();
                DesktopCapture.Show(root);
                var deadline = DateTime.UtcNow.AddSeconds(15);
                while (!DesktopCapture.IsAttached && DateTime.UtcNow < deadline) await Task.Delay(100);
                if (!DesktopCapture.IsAttached) throw new Exception("Capture helper was not embedded in the main window.");
                var capturePage = root.Children.OfType<UserControl>().Single(item => item.Name == "DesktopCapturePage");
                if (root.Children.OfType<Grid>().Any(item => item.Name == "DialogOverlay")) throw new Exception("Capture must be a workspace subpage, like slide review, rather than a modal overlay.");
                if (page.Visibility != Visibility.Collapsed || hidden.Visibility != Visibility.Collapsed || title.Visibility != Visibility.Visible)
                    throw new Exception("Capture must replace the workspace while preserving the title bar.");
                if (Grid.GetRow(capturePage) != 1 || Grid.GetRowSpan(capturePage) != 2 || Grid.GetColumnSpan(capturePage) != 4)
                    throw new Exception("Capture must occupy the same full workspace as slide review.");
                var back = ((DockPanel)capturePage.Content).Children.OfType<Button>().Single(item => item.Name == "BackToLesson");
                back.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                deadline = DateTime.UtcNow.AddSeconds(8);
                while (DesktopCapture.IsOpen && DateTime.UtcNow < deadline) await Task.Delay(100);
                if (DesktopCapture.IsOpen) throw new Exception("Embedded capture did not close cleanly.");
                if (root.Children.Contains(capturePage) || page.Visibility != Visibility.Visible || hidden.Visibility != Visibility.Hidden)
                    throw new Exception("Back must remove the capture page and restore exact workspace visibility.");
                DesktopCapture.Show(root);
                deadline = DateTime.UtcNow.AddSeconds(15);
                while (!DesktopCapture.IsAttached && DateTime.UtcNow < deadline) await Task.Delay(100);
                if (!DesktopCapture.IsAttached) throw new Exception("Capture page did not reopen.");
                capturePage = root.Children.OfType<UserControl>().Single(item => item.Name == "DesktopCapturePage");
                capturePage.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice, PresentationSource.FromVisual(window), 0, Key.Escape) { RoutedEvent = Keyboard.PreviewKeyDownEvent });
                deadline = DateTime.UtcNow.AddSeconds(8);
                while (DesktopCapture.IsOpen && DateTime.UtcNow < deadline) await Task.Delay(100);
                if (DesktopCapture.IsOpen || page.Visibility != Visibility.Visible) throw new Exception("Escape must return to the lesson.");
                Console.WriteLine("PASS: capture replaces workspace like slide review, embeds its helper, preserves title bar, restores visibility on Back, and reopens/closes with Escape; no recording started.");
                result = 0;
            }
            catch (Exception error) { Console.Error.WriteLine(error); }
            finally { app.Shutdown(result); }
        };
        app.Run(window); return result;
    }
}
