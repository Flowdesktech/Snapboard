using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Interop;
using System.Windows.Media.Imaging;

namespace Snapboard.Helpers;

/// <summary>
/// Captures the entire virtual desktop (all connected monitors) to a GDI bitmap
/// and exposes helpers for WPF interop and optional cursor drawing.
/// </summary>
public static class ScreenCapture
{
    [DllImport("gdi32.dll")]
    private static extern bool DeleteObject(IntPtr hObject);

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT { public int X; public int Y; }

    [StructLayout(LayoutKind.Sequential)]
    private struct CURSORINFO
    {
        public int cbSize;
        public int flags;
        public IntPtr hCursor;
        public POINT ptScreenPos;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct ICONINFO
    {
        public bool fIcon;
        public int xHotspot;
        public int yHotspot;
        public IntPtr hbmMask;
        public IntPtr hbmColor;
    }

    [DllImport("user32.dll")]
    private static extern bool GetCursorInfo(out CURSORINFO pci);

    [DllImport("user32.dll")]
    private static extern bool GetIconInfo(IntPtr hIcon, out ICONINFO piconinfo);

    private const int CURSOR_SHOWING = 0x00000001;

    public static Bitmap CapturePrimaryScreen(bool includeCursor = false)
    {
        var screen = Screen.PrimaryScreen ?? throw new InvalidOperationException("No primary screen found.");
        return CaptureBounds(screen.Bounds, includeCursor);
    }

    public static Bitmap CaptureVirtualScreen(out Rectangle virtualBounds, bool includeCursor = false)
    {
        virtualBounds = SystemInformation.VirtualScreen;
        return CaptureBounds(virtualBounds, includeCursor);
    }

    /// <summary>Captures any screen-pixel rectangle (physical pixels on the
    /// virtual desktop). Used by the scrolling capture stitcher.</summary>
    public static Bitmap CaptureScreenRegion(Rectangle bounds, bool includeCursor = false)
        => CaptureBounds(bounds, includeCursor);

    private static Bitmap CaptureBounds(Rectangle bounds, bool includeCursor)
    {
        var bmp = new Bitmap(bounds.Width, bounds.Height, PixelFormat.Format32bppArgb);
        using (var g = Graphics.FromImage(bmp))
        {
            g.CopyFromScreen(bounds.Location, System.Drawing.Point.Empty, bounds.Size, CopyPixelOperation.SourceCopy);
            if (includeCursor)
            {
                DrawCursor(g, bounds);
            }
        }
        return bmp;
    }

    private static void DrawCursor(Graphics g, Rectangle bounds)
    {
        var ci = new CURSORINFO { cbSize = Marshal.SizeOf<CURSORINFO>() };
        if (!GetCursorInfo(out ci) || (ci.flags & CURSOR_SHOWING) == 0 || ci.hCursor == IntPtr.Zero)
        {
            return;
        }

        int hotX = 0, hotY = 0;
        if (GetIconInfo(ci.hCursor, out var info))
        {
            hotX = info.xHotspot;
            hotY = info.yHotspot;
            if (info.hbmMask  != IntPtr.Zero) DeleteObject(info.hbmMask);
            if (info.hbmColor != IntPtr.Zero) DeleteObject(info.hbmColor);
        }

        int x = ci.ptScreenPos.X - bounds.X - hotX;
        int y = ci.ptScreenPos.Y - bounds.Y - hotY;

        try
        {
            using var icon = Icon.FromHandle(ci.hCursor);
            g.DrawIcon(icon, new Rectangle(x, y, icon.Width, icon.Height));
        }
        catch
        {
            // Some special cursors (e.g. DWM animated) can't be wrapped as Icon. Skip.
        }
    }

    /// <summary>
    /// Converts a GDI Bitmap to a freezable WPF BitmapSource (safe for cross-thread use after Freeze()).
    /// </summary>
    public static BitmapSource ToBitmapSource(Bitmap bmp)
    {
        var hbmp = bmp.GetHbitmap();
        try
        {
            var source = Imaging.CreateBitmapSourceFromHBitmap(
                hbmp,
                IntPtr.Zero,
                Int32Rect.Empty,
                BitmapSizeOptions.FromEmptyOptions());
            source.Freeze();
            return source;
        }
        finally
        {
            DeleteObject(hbmp);
        }
    }
}
