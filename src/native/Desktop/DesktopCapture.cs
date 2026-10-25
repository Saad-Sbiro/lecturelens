using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms.Integration;
using System.Windows.Media;
using System.Windows.Input;
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
        if (active is not null || view is not Grid shell) return;
        var previousFocus = Keyboard.FocusedElement;
        var workspace = shell.Children.Cast<UIElement>().Where(element => Grid.GetRow(element) > 0)
            .Select(element => (Element: element, element.Visibility)).ToArray();
        var page = new UserControl { Name = "DesktopCapturePage", Focusable = true };
        foreach (var type in new[] { typeof(Button), typeof(TextBlock) })
            if (view.TryFindResource(type) is Style style) page.Resources[type] = style;
        page.SetResourceReference(Control.BackgroundProperty, "AppBackground");
        page.SetResourceReference(Control.ForegroundProperty, "AppForeground");
        page.SetResourceReference(Control.FontFamilyProperty, "AppFont");
        var content = new DockPanel { Margin = new Thickness(24) };
        var backContent = new StackPanel { Orientation = Orientation.Horizontal };
        var arrow = new System.Windows.Shapes.Path { Data = Geometry.Parse("M14,6 L8,12 L14,18 M8,12 H21"), StrokeThickness = 2,
            StrokeStartLineCap = PenLineCap.Round, StrokeEndLineCap = PenLineCap.Round, StrokeLineJoin = PenLineJoin.Round };
        arrow.SetResourceReference(System.Windows.Shapes.Shape.StrokeProperty, "AppForeground");
        var icon = new Canvas { Width = 24, Height = 24 }; icon.Children.Add(arrow);
        backContent.Children.Add(new Viewbox { Width = 16, Height = 16, Margin = new Thickness(0, 0, 7, 0), Child = icon });
        backContent.Children.Add(new TextBlock { Text = (string)view.FindResource("backToLesson") });
        var back = new Button { Name = "BackToLesson", Content = backContent, HorizontalAlignment = HorizontalAlignment.Left,
            Margin = new Thickness(0, 0, 0, 18), Padding = new Thickness(12, 9, 12, 9) };
        DockPanel.SetDock(back, Dock.Top); content.Children.Add(back);
        var header = new StackPanel { Margin = new Thickness(0, 0, 0, 18) };
        header.Children.Add(new TextBlock { Text = (string)view.FindResource("desktopCaptureTitle"), FontSize = 26, FontWeight = FontWeights.Bold });
        var hint = new TextBlock { Text = (string)view.FindResource("desktopCaptureHint"), Margin = new Thickness(0, 7, 0, 0) };
        hint.SetResourceReference(TextBlock.ForegroundProperty, "AppMuted"); header.Children.Add(hint);
        DockPanel.SetDock(header, Dock.Top); content.Children.Add(header);
        var capture = new EmbeddedCapture(view);
        content.Children.Add(capture); page.Content = content;
        active = capture;
        back.Click += (_, _) => capture.RequestClose();
        page.PreviewKeyDown += (_, e) => { if (e.Key == Key.Escape) { capture.RequestClose(); e.Handled = true; } };
        capture.Exited = () => view.Dispatcher.InvokeAsync(() => {
            capture.Dispose(); shell.Children.Remove(page);
            foreach (var item in workspace) item.Element.Visibility = item.Visibility;
            active = null;
            if (previousFocus is not null) Keyboard.Focus(previousFocus);
        });
        foreach (var item in workspace) item.Element.Visibility = Visibility.Collapsed;
        Grid.SetRow(page, 1); Grid.SetRowSpan(page, 2); Grid.SetColumnSpan(page, 4);
        shell.Children.Add(page); page.Focus();
        ControlMotion.AttachTree(page);
    }
}

internal sealed partial class EmbeddedCapture : Grid, IDisposable
{
    const int StyleIndex = -16;
    const long ChildStyle = 0x40000000L;
    const long PopupStyle = 0x80000000L;
    readonly Forms.Panel panel = new() { Dock = Forms.DockStyle.Fill, BackColor = System.Drawing.Color.FromArgb(21, 23, 27) };
    readonly WindowsFormsHost host;
    readonly TextBlock status = new() { HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
    Process? process;
    nint childWindow;
    bool closing;
    readonly string theme, font;
    internal Action? Exited { get; set; }
    internal bool Attached => childWindow != 0 && GetParent(childWindow) == panel.Handle;

    internal EmbeddedCapture(FrameworkElement view)
    {
        ClipToBounds = true;
        var background = (SolidColorBrush)view.FindResource("AppBackground");
        Background = background;
        panel.BackColor = System.Drawing.Color.FromArgb(background.Color.R, background.Color.G, background.Color.B);
        theme = background.Color.R > 128 ? "light" : "dark";
        font = ((FontFamily)view.FindResource("AppFont")).Source;
        status.SetResourceReference(TextBlock.TextProperty, "desktopCaptureOpening");
        status.SetResourceReference(TextBlock.ForegroundProperty, "AppMuted");
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
            start.ArgumentList.Add("--theme=" + theme); start.ArgumentList.Add("--font=" + font);
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
