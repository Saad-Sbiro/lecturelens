using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace LectureLens.Desktop;

// Borderless maximization must use the current monitor's work area, not its full bounds.
internal static class WindowBounds
{
    internal static void Attach(Window window)
    {
        window.SourceInitialized += (_, _) =>
        {
            var handle = new WindowInteropHelper(window).Handle;
            HwndSource.FromHwnd(handle).AddHook(Hook);
            var color = unchecked((int)0xFFFFFFFE); // DWMWA_COLOR_NONE removes the native border.
            DwmSetWindowAttribute(handle, 34, ref color, sizeof(int));
        };
    }

    private static nint Hook(nint hwnd, int message, nint wParam, nint lParam, ref bool handled)
    {
        if (message != 0x0024) return 0; // WM_GETMINMAXINFO
        var monitor = MonitorFromWindow(hwnd, 2);
        var info = new MonitorInfo { Size = Marshal.SizeOf<MonitorInfo>() };
        if (!GetMonitorInfo(monitor, ref info)) return 0;
        var bounds = Marshal.PtrToStructure<MinMaxInfo>(lParam);
        bounds.MaxPosition = new Point { X = info.Work.Left - info.Monitor.Left, Y = info.Work.Top - info.Monitor.Top };
        bounds.MaxSize = new Point { X = info.Work.Right - info.Work.Left, Y = info.Work.Bottom - info.Work.Top };
        Marshal.StructureToPtr(bounds, lParam, false);
        handled = true;
        return 0;
    }

    [StructLayout(LayoutKind.Sequential)] private struct Point { public int X, Y; }
    [StructLayout(LayoutKind.Sequential)] private struct Rect { public int Left, Top, Right, Bottom; }
    [StructLayout(LayoutKind.Sequential)] private struct MonitorInfo { public int Size; public Rect Monitor, Work; public int Flags; }
    [StructLayout(LayoutKind.Sequential)] private struct MinMaxInfo { public Point Reserved, MaxSize, MaxPosition, MinTrackSize, MaxTrackSize; }
    [DllImport("user32.dll")] private static extern nint MonitorFromWindow(nint hwnd, uint flags);
    [DllImport("user32.dll", CharSet = CharSet.Auto)] [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetMonitorInfo(nint monitor, ref MonitorInfo info);
    [DllImport("dwmapi.dll")] private static extern int DwmSetWindowAttribute(nint hwnd, int attribute, ref int value, int size);
}
