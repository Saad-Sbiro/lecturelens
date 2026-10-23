using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace LectureLens.Desktop;

internal static class TaskbarIcon
{
    internal static BitmapSource Create()
    {
        var original = BitmapFrame.Create(new Uri("pack://application:,,,/LectureLens;component/assets/logomark.png"));
        var source = new FormatConvertedBitmap(original, PixelFormats.Bgra32, null, 0);
        int width = source.PixelWidth, height = source.PixelHeight;
        var pixels = new byte[width * height * 4]; source.CopyPixels(pixels, width * 4, 0);
        int left = width, top = height, right = 0, bottom = 0;
        for (int y = 0; y < height; y++) for (int x = 0; x < width; x++)
        {
            if (pixels[(y * width + x) * 4 + 3] < 32) continue;
            left = Math.Min(left, x); right = Math.Max(right, x); top = Math.Min(top, y); bottom = Math.Max(bottom, y);
        }
        if (left > right) return original;
        // Fill the taskbar icon without stretching the mark or changing the in-app logo.
        int size = Math.Min(Math.Min(width, height), (int)(Math.Max(right - left + 1, bottom - top + 1) * 1.12));
        int originX = Math.Clamp((left + right - size) / 2, 0, width - size);
        int originY = Math.Clamp((top + bottom - size) / 2, 0, height - size);
        var icon = new CroppedBitmap(source, new Int32Rect(originX, originY, size, size)); icon.Freeze(); return icon;
    }
}
