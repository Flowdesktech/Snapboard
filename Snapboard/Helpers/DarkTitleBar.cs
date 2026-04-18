using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace Snapboard.Helpers;

/// <summary>
/// Flips a WPF window's native title-bar to dark mode via DWM.
/// Call <see cref="Apply(Window)"/> once in the window's constructor.
/// </summary>
public static class DarkTitleBar
{
    // DWM attribute for the immersive dark-mode title bar.
    // 20 on Windows 10 20H1+ / Windows 11.
    // 19 on older Windows 10 insider builds (1903-1909). Try both.
    private const int DWMWA_USE_IMMERSIVE_DARK_MODE         = 20;
    private const int DWMWA_USE_IMMERSIVE_DARK_MODE_LEGACY  = 19;

    [DllImport("dwmapi.dll", PreserveSig = true)]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

    /// <summary>
    /// Applies a dark title bar to the given window. Safe to call on any
    /// Windows version — silently no-ops on versions that don't support it.
    /// </summary>
    public static void Apply(Window window)
    {
        if (window == null) return;

        void Set()
        {
            var hwnd = new WindowInteropHelper(window).Handle;
            if (hwnd == IntPtr.Zero) return;

            int useDark = 1;
            int hr = DwmSetWindowAttribute(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE, ref useDark, sizeof(int));
            if (hr != 0)
            {
                // Older Windows 10 builds only know the 19 attribute.
                DwmSetWindowAttribute(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE_LEGACY, ref useDark, sizeof(int));
            }
        }

        // The HWND only exists after SourceInitialized. If we're already past it
        // (e.g. window already shown), apply immediately.
        if (new WindowInteropHelper(window).Handle != IntPtr.Zero)
        {
            Set();
        }
        else
        {
            window.SourceInitialized += (_, _) => Set();
        }
    }
}
