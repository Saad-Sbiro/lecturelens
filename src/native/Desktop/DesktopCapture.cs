using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms.Integration;
using System.Windows.Media;
using Forms = System.Windows.Forms;

namespace LectureLens.Desktop;

internal static class DesktopCapture
{
    static EmbeddedCapture? active;
    internal static bool IsOpen => active is not null;
    internal static bool IsAttached => active?.Attached == true;
    internal static void Close() => active?.RequestClose();

    internal static void Show(FrameworkElement view)
    {
        if (active is not null) return;
        var content = new Grid { Height = 700, MinHeight = 520 };
        content.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        content.RowDefinitions.Add(new RowDefinition());

        var header = new DockPanel { Margin = new Thickness(0, 0, 0, 14) };
        var close = new Button {
            Content = (string)view.FindResource("desktopCaptureClose"),
            Style = (Style)view.FindResource("Quiet"), Padding = new Thickness(12, 8, 12, 8),
            Margin = new Thickness(16, 0, 0, 0)
        };
        DockPanel.SetDock(close, Dock.Right); header.Children.Add(close);
        header.Children.Add(new TextBlock {
            Text = (string)view.FindResource("desktopCaptureTitle"), FontSize = 20,
            FontWeight = FontWeights.SemiBold, VerticalAlignment = VerticalAlignment.Center
        });
        content.Children.Add(header);

        var capture = new EmbeddedCapture();
        Grid.SetRow(capture, 1); content.Children.Add(capture);
        var dialog = new InAppDialog(view, content, 1180);
        active = capture;
        void Dismiss() => capture.RequestClose();
        dialog.Dismiss = Dismiss; close.Click += (_, _) => Dismiss();
        capture.Exited = () => view.Dispatcher.InvokeAsync(() => {
            capture.Dispose(); dialog.Close(); active = null;
        });
    }
}

internal sealed partial class EmbeddedCapture : Grid, IDisposable
{
    const int StyleIndex = -16;
    const long ChildStyle = 0x40000000L;
    const long PopupStyle = 0x80000000L;
    readonly Forms.Panel panel = new() { Dock = Forms.DockStyle.Fill, BackColor = System.Drawing.Color.FromArgb(21, 23, 27) };
    readonly WindowsFormsHost host;
    readonly TextBlock status = new() { Text = "Opening desktop capture…", HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
    Process? process;
    nint childWindow;
    bool closing;
    internal Action? Exited { get; set; }
    internal bool Attached => childWindow != 0 && GetParent(childWindow) == panel.Handle;

    internal EmbeddedCapture()
    {
        ClipToBounds = true;
        Background = new SolidColorBrush(Color.FromRgb(21, 23, 27));
        host = new WindowsFormsHost { Child = panel };
        Children.Add(host); Children.Add(status);
        Loaded += async (_, _) => await Start();
        panel.Resize += (_, _) => ResizeChild();
    }

    async Task Start()
    {
        if (process is not null) return;
        try
        {
            var runtime = Environment.GetEnvironmentVariable("LECTURELENS_CAPTURE_RUNTIME") ?? Path.Combine(AppContext.BaseDirectory, "capture-runtime", "electron.exe");
            var entry = Environment.GetEnvironmentVariable("LECTURELENS_CAPTURE_ENTRY") ?? Path.Combine(AppContext.BaseDirectory, "desktop-capture", "main.cjs");
            if (!File.Exists(runtime) || !File.Exists(entry)) throw new FileNotFoundException("Desktop capture is missing. Run npm run dev or rebuild the native package.");
            var start = new ProcessStartInfo(runtime) { UseShellExecute = false, CreateNoWindow = true };
            start.Environment.Remove("ELECTRON_RUN_AS_NODE");
            start.ArgumentList.Add(entry); start.ArgumentList.Add("--embedded");
            process = Process.Start(start) ?? throw new InvalidOperationException("Desktop capture could not start.");
            process.EnableRaisingEvents = true;
            process.Exited += (_, _) => Dispatcher.BeginInvoke(() => Exited?.Invoke());
            childWindow = await Task.Run(() => WaitForWindow(process));
            if (closing || process.HasExited) return;
            var style = GetWindowLongPtr(childWindow, StyleIndex).ToInt64();
            SetWindowLongPtr(childWindow, StyleIndex, new nint((style & ~PopupStyle) | ChildStyle));
            SetParent(childWindow, panel.Handle);
            if (GetParent(childWindow) != panel.Handle)
                throw new InvalidOperationException("Desktop capture could not attach to LectureLens.");
            ShowWindow(childWindow, 5); ResizeChild(); status.Visibility = Visibility.Collapsed;
        }
        catch (Exception error)
        {
            status.Text = error.Message;
            status.Foreground = new SolidColorBrush(Color.FromRgb(219, 157, 155));
        }
    }

    static nint WaitForWindow(Process child)
    {
        var deadline = DateTime.UtcNow.AddSeconds(12);
        while (!child.HasExited && DateTime.UtcNow < deadline)
        {
            child.Refresh();
            if (child.MainWindowHandle != 0) return child.MainWindowHandle;
            Thread.Sleep(50);
        }
        throw new InvalidOperationException("Desktop capture did not become ready.");
    }

    void ResizeChild()
    {
        if (childWindow != 0) MoveWindow(childWindow, 0, 0, Math.Max(1, panel.ClientSize.Width), Math.Max(1, panel.ClientSize.Height), true);
    }

    internal void RequestClose()
    {
        if (closing) return;
        if (process is null || process.HasExited) { Exited?.Invoke(); return; }
        if (childWindow == 0) { process.Kill(); return; }
        process.CloseMainWindow();
    }

    public void Dispose()
    {
        closing = true; childWindow = 0;
        if (process?.HasExited == true) process.Dispose();
        host.Dispose(); panel.Dispose();
    }

    [DllImport("user32.dll", SetLastError = true)] static extern nint SetParent(nint child, nint parent);
    [DllImport("user32.dll")] static extern nint GetParent(nint child);
    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW", SetLastError = true)] static extern nint GetWindowLongPtr(nint window, int index);
    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW", SetLastError = true)] static extern nint SetWindowLongPtr(nint window, int index, nint value);
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] static extern bool MoveWindow(nint window, int x, int y, int width, int height, [MarshalAs(UnmanagedType.Bool)] bool repaint);
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] static extern bool ShowWindow(nint window, int command);
}
