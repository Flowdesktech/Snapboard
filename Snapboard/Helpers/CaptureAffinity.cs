using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace Snapboard.Helpers;

/// <summary>
/// Marks a WPF window as excluded from screen capture so it doesn't appear
/// in screenshots we take of other windows. Uses
/// <c>SetWindowDisplayAffinity(WDA_EXCLUDEFROMCAPTURE)</c> — available on
/// Windows 10 version 2004 (build 19041) and later. On older builds the
/// call is silently ignored.
///
/// This is what lets Snapboard's scrolling-capture session window stay
/// visible on-screen (showing status / stop button) while its pixels are
/// completely omitted from every frame we grab of the target behind it —
/// no flicker, no hiding, no per-frame repositioning dance.
/// </summary>
public static class CaptureAffinity
{
    private const uint WDA_NONE               = 0x00000000;
    private const uint WDA_EXCLUDEFROMCAPTURE = 0x00000011;

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SetWindowDisplayAffinity(IntPtr hWnd, uint dwAffinity);

    /// <summary>
    /// Hides the given WPF window from all screen-capture APIs (GDI
    /// <c>BitBlt</c>, <c>PrintWindow</c>, Windows.Graphics.Capture, Print
    /// Screen, etc.). Safe to call before the HWND exists — it'll be
    /// applied as soon as <c>SourceInitialized</c> fires.
    /// </summary>
    public static void ExcludeFromCapture(Window window)
    {
        if (window == null) return;

        void Apply()
        {
            var hwnd = new WindowInteropHelper(window).Handle;
            if (hwnd == IntPtr.Zero) return;
            try { SetWindowDisplayAffinity(hwnd, WDA_EXCLUDEFROMCAPTURE); }
            catch { /* silently no-op on unsupported builds */ }
        }

        if (new WindowInteropHelper(window).Handle != IntPtr.Zero)
        {
            Apply();
        }
        else
        {
            window.SourceInitialized += (_, _) => Apply();
        }
    }
}
