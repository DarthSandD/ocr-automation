using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Text;
using OcrAutomation.Models;
using OcrAutomation.Services.Interfaces;
using OcrAutomation.Utilities;

namespace OcrAutomation.Services;

public class ScreenCaptureService : IScreenCaptureService
{
    [DllImport("user32.dll")]
    private static extern IntPtr GetDesktopWindow();

    [DllImport("user32.dll")]
    private static extern IntPtr GetWindowDC(IntPtr hWnd);

    [DllImport("gdi32.dll")]
    private static extern bool BitBlt(IntPtr hdcDest, int xDest, int yDest, int wDest, int hDest,
        IntPtr hdcSrc, int xSrc, int ySrc, int rop);

    [DllImport("gdi32.dll")]
    private static extern IntPtr CreateCompatibleDC(IntPtr hdc);

    [DllImport("gdi32.dll")]
    private static extern IntPtr CreateCompatibleBitmap(IntPtr hdc, int width, int height);

    [DllImport("gdi32.dll")]
    private static extern IntPtr SelectObject(IntPtr hdc, IntPtr hgdiobj);

    [DllImport("gdi32.dll")]
    private static extern bool DeleteObject(IntPtr hObject);

    [DllImport("gdi32.dll")]
    private static extern bool DeleteDC(IntPtr hdc);

    [DllImport("user32.dll")]
    private static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);

    private const int SRCCOPY = 0x00CC0020;

    public async Task<List<WindowInfo>> EnumerateWindowsAsync()
    {
        return await Task.Run(() =>
        {
            var windows = new List<WindowInfo>();

            Win32Api.EnumWindows((hWnd, lParam) =>
            {
                if (!Win32Api.IsWindowVisible(hWnd))
                    return true;

                int length = Win32Api.GetWindowTextLength(hWnd);
                var title = length > 0
                    ? new StringBuilder(length + 1).Apply(_ => Win32Api.GetWindowText(hWnd, _, _.Capacity)).ToString()
                    : string.Empty;

                if (string.IsNullOrWhiteSpace(title))
                    return true;

                // Skip tool windows and invisible windows
                int exStyle = Win32Api.GetWindowLong(hWnd, Win32Api.GWL_EXSTYLE);
                if ((exStyle & 0x00000080) != 0) // WS_EX_TOOLWINDOW
                    return true;

                var myProcessId = System.Diagnostics.Process.GetCurrentProcess().Id;
                Win32Api.GetWindowThreadProcessId(hWnd, out int processId);
                if (processId == myProcessId) return true;

                // Get window rect for bounds
                Win32Api.GetWindowRect(hWnd, out var rect);

                var windowInfo = new WindowInfo
                {
                    Handle = hWnd,
                    Title = title,
                    ProcessId = processId,
                    IsElevated = ProcessElevationChecker.IsProcessElevated(processId),
                    Bounds = new CaptureRegion
                    {
                        X = rect.Left,
                        Y = rect.Top,
                        Width = rect.Right - rect.Left,
                        Height = rect.Bottom - rect.Top
                    }
                };

                windows.Add(windowInfo);
                return true;
            }, IntPtr.Zero);

            return windows.OrderBy(w => w.Title).ToList();
        });
    }

    public async Task<Bitmap> CaptureWindowAsync(IntPtr windowHandle)
    {
        return await Task.Run(() =>
        {
            if (!Win32Api.IsWindow(windowHandle))
                throw new InvalidOperationException("Invalid window handle.");

            if (!Win32Api.IsWindowVisible(windowHandle))
                throw new InvalidOperationException("Window is not visible.");

            // Get window rect in screen coordinates
            if (!Win32Api.GetWindowRect(windowHandle, out var rect))
                throw new InvalidOperationException("Failed to get window rectangle.");

            int width = rect.Right - rect.Left;
            int height = rect.Bottom - rect.Top;

            if (width <= 0 || height <= 0)
                throw new InvalidOperationException($"Invalid window dimensions: {width}x{height}");

            // Handle DPI scaling - get DPI for window's monitor
            uint dpi = Win32Api.GetDpiForWindow(windowHandle);
            float dpiScale = dpi / 96.0f;

            int scaledWidth = (int)(width * dpiScale);
            int scaledHeight = (int)(height * dpiScale);

            // Use GDI BitBlt for window capture
            var bitmap = CaptureWindowGdi(windowHandle, rect, dpiScale);

            return bitmap;
        });
    }

    private Bitmap CaptureWindowGdi(IntPtr hWnd, Utilities.Win32Api.RECT rect, float dpiScale)
    {
        int width = rect.Right - rect.Left;
        int height = rect.Bottom - rect.Top;

        IntPtr hdcScreen = IntPtr.Zero;
        IntPtr hdcMemDC = IntPtr.Zero;
        IntPtr hBitmap = IntPtr.Zero;
        IntPtr hOldBitmap = IntPtr.Zero;

        try
        {
            hdcScreen = Win32Api.GetDC(hWnd);
            hdcMemDC = Win32Api.CreateCompatibleDC(hdcScreen);
            hBitmap = Win32Api.CreateCompatibleBitmap(hdcScreen, width, height);
            hOldBitmap = Win32Api.SelectObject(hdcMemDC, hBitmap);

            // Use PrintWindow to capture the window content (works better than BitBlt for some windows)
            if (!Win32Api.PrintWindow(hWnd, hdcMemDC, 0))
            {
                // Fallback to BitBlt
                Win32Api.BitBlt(hdcMemDC, 0, 0, width, height, hdcScreen, rect.Left, rect.Top, SRCCOPY);
            }

            Win32Api.SelectObject(hdcMemDC, hOldBitmap);

            var bitmap = Image.FromHbitmap(hBitmap);

            // Scale if DPI scaling is active
            if (Math.Abs(dpiScale - 1.0f) > 0.01f)
            {
                var scaled = new Bitmap((int)(bitmap.Width / dpiScale), (int)(bitmap.Height / dpiScale));
                using (var g = Graphics.FromImage(scaled))
                {
                    g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                    g.DrawImage(bitmap, 0, 0, scaled.Width, scaled.Height);
                }
                bitmap.Dispose();
                return scaled;
            }

            return bitmap;
        }
        finally
        {
            if (hBitmap != IntPtr.Zero) Win32Api.DeleteObject(hBitmap);
            if (hdcMemDC != IntPtr.Zero) Win32Api.DeleteDC(hdcMemDC);
            if (hdcScreen != IntPtr.Zero) Win32Api.ReleaseDC(hWnd, hdcScreen);
        }
    }

    public async Task<Bitmap> CaptureRegionAsync(CaptureRegion region)
    {
        return await Task.Run(() =>
        {
            if (region.Width <= 0 || region.Height <= 0)
                throw new InvalidOperationException("Invalid region dimensions.");

            // Get DPI for the monitor containing this region
            IntPtr hMonitor = MonitorFromPoint(new POINT(region.X + region.Width / 2, region.Y + region.Height / 2), 2);
            uint dpiX = 96, dpiY = 96;
            GetDpiForMonitor(hMonitor, 0, out dpiX, out dpiY);
            float dpiScale = dpiX / 96.0f;

            int scaledWidth = (int)(region.Width * dpiScale);
            int scaledHeight = (int)(region.Height * dpiScale);

            using var bitmap = new Bitmap(scaledWidth, scaledHeight, PixelFormat.Format32bppArgb);
            using var source = CaptureScreenPortion(region.X, region.Y, region.Width, region.Height);
            using (var graphics = Graphics.FromImage(bitmap))
            {
                graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                graphics.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.HighQuality;
                graphics.DrawImage(source, 0, 0, scaledWidth, scaledHeight);
            }
            return new Bitmap(bitmap);
        });
    }

    private Bitmap CaptureScreenPortion(int x, int y, int width, int height)
    {
        var bitmap = new Bitmap(width, height, PixelFormat.Format32bppArgb);
        using (var graphics = Graphics.FromImage(bitmap))
        {
            graphics.CopyFromScreen(x, y, 0, 0, new Size(width, height));
        }
        return bitmap;
    }

    public Task<CaptureRegion?> SelectRegionAsync()
    {
        // This method launches the region selector overlay window
        // The overlay captures mouse input to define a rectangle
        var selector = new Utilities.RegionSelectorWindow();
        var result = selector.ShowDialog();

        CaptureRegion? selectedRegion = result == true
            ? selector.SelectedRegion
            : null;

        return Task.FromResult(selectedRegion);
    }

    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromPoint(POINT pt, uint dwFlags);

    [DllImport("shcore.dll", PreserveSig = false)]
    private static extern void GetDpiForMonitor(IntPtr hMonitor, int dpiType, out uint dpiX, out uint dpiY);
}

// Extension methods
public static class StringBuilderExtensions
{
    public static StringBuilder Apply(this StringBuilder sb, Action<StringBuilder> action)
    {
        action(sb);
        return sb;
    }
}

public struct RECT
{
    public int Left, Top, Right, Bottom;
}

public struct POINT
{
    public int X, Y;
    public POINT(int x, int y) { X = x; Y = y; }
}
