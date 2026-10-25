using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
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
        root.Resources["desktopCaptureClose"] = "Close capture";
        root.Resources["PanelBackground"] = System.Windows.Media.Brushes.Black;
        root.Resources["Quiet"] = new Style(typeof(Button));
        var page = new Border(); Grid.SetRow(page, 1); root.Children.Add(page);
        var window = new Window { Content = root, Width = 1200, Height = 850, Left = -12000, Top = -12000, ShowInTaskbar = false, WindowStyle = WindowStyle.None };
        var result = 1;
        window.ContentRendered += async (_, _) => {
            try
            {
                DesktopCapture.Show(root);
                var deadline = DateTime.UtcNow.AddSeconds(15);
                while (!DesktopCapture.IsAttached && DateTime.UtcNow < deadline) await Task.Delay(100);
                if (!DesktopCapture.IsAttached) throw new Exception("Capture helper was not embedded in the main window.");
                if (!root.Children.OfType<Grid>().Any(item => item.Name == "DialogOverlay")) throw new Exception("Capture page is not inside the in-app overlay.");
                DesktopCapture.Close();
                deadline = DateTime.UtcNow.AddSeconds(8);
                while (DesktopCapture.IsOpen && DateTime.UtcNow < deadline) await Task.Delay(100);
                if (DesktopCapture.IsOpen) throw new Exception("Embedded capture did not close cleanly.");
                Console.WriteLine("PASS: desktop capture attaches inside the LectureLens overlay and closes without a separate app window.");
                result = 0;
            }
            catch (Exception error) { Console.Error.WriteLine(error); }
            finally { app.Shutdown(result); }
        };
        app.Run(window); return result;
    }
}
