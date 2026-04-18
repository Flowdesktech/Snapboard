using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using SD = System.Drawing;
using SDI = System.Drawing.Imaging;

namespace Snapboard.Helpers;

/// <summary>
/// Enumerates top-level visible windows and captures them via <c>PrintWindow</c>
/// with the <c>PW_RENDERFULLCONTENT</c> flag so hardware-accelerated apps
/// (Chromium, WPF, UWP, etc.) render correctly.
/// </summary>
public static class WindowEnumerator
{
    // ---------------- Win32 ----------------

    private delegate bool EnumWindowsProc(IntPtr hwnd, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern bool IsWindowVisible(IntPtr hwnd);

    [DllImport("user32.dll")]
    private static extern bool IsIconic(IntPtr hwnd);

    [DllImport("user32.dll")]
    private static extern IntPtr GetShellWindow();

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern int GetWindowText(IntPtr hwnd, StringBuilder lpString, int nMaxCount);

    [DllImport("user32.dll")]
    private static extern int GetWindowTextLength(IntPtr hwnd);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hwnd, out uint lpdwProcessId);

    [DllImport("user32.dll")]
    private static extern bool GetWindowRect(IntPtr hwnd, out RECT lpRect);

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern int GetClassName(IntPtr hwnd, StringBuilder lpClassName, int nMaxCount);

    [DllImport("user32.dll")]
    private static extern bool PrintWindow(IntPtr hwnd, IntPtr hdc, uint flags);

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr hwnd, int cmd);

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hwnd);

    [DllImport("dwmapi.dll")]
    private static extern int DwmGetWindowAttribute(IntPtr hwnd, int dwAttribute, out int pvAttribute, int cbAttribute);

    [DllImport("dwmapi.dll")]
    private static extern int DwmGetWindowAttribute(IntPtr hwnd, int dwAttribute, out RECT pvAttribute, int cbAttribute);

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT { public int Left, Top, Right, Bottom; }

    private const int DWMWA_CLOAKED = 14;
    private const int DWMWA_EXTENDED_FRAME_BOUNDS = 9;
    private const uint PW_RENDERFULLCONTENT = 0x00000002;
    private const int SW_RESTORE = 9;

    // ---------------- Public API ----------------

    public sealed record WindowInfo(
        IntPtr Handle,
        string Title,
        string ProcessName,
        int ProcessId,
        SD.Rectangle Bounds,
        bool IsMinimized,
        SD.Icon? Icon);

    /// <summary>
    /// Lists every visible, non-cloaked, non-minimized top-level window with a
    /// non-empty title and reasonable bounds, skipping Snapboard's own windows
    /// and the shell (Progman).
    /// </summary>
    public static List<WindowInfo> EnumerateTopLevelWindows(bool includeMinimized = true)
    {
        var results = new List<WindowInfo>();
        IntPtr shell = GetShellWindow();
        int selfPid = Environment.ProcessId;

        EnumWindows((hwnd, _) =>
        {
            try
            {
                if (hwnd == shell) return true;
                if (!IsWindowVisible(hwnd)) return true;

                if (DwmGetWindowAttribute(hwnd, DWMWA_CLOAKED, out int cloaked, sizeof(int)) == 0
                    && cloaked != 0)
                {
                    return true;
                }

                int titleLen = GetWindowTextLength(hwnd);
                if (titleLen == 0) return true;
                var sb = new StringBuilder(titleLen + 1);
                GetWindowText(hwnd, sb, sb.Capacity);
                string title = sb.ToString();
                if (string.IsNullOrWhiteSpace(title)) return true;

                bool minimized = IsIconic(hwnd);
                if (minimized && !includeMinimized) return true;

                if (!TryGetWindowBounds(hwnd, out var bounds)) return true;
                if (bounds.Width < 80 || bounds.Height < 40) return true;

                GetWindowThreadProcessId(hwnd, out uint pid);
                if (pid == 0) return true;
                if ((int)pid == selfPid) return true;

                string processName = "";
                SD.Icon? icon = null;
                try
                {
                    using var p = Process.GetProcessById((int)pid);
                    processName = p.ProcessName;
                    string? exe = null;
                    try { exe = p.MainModule?.FileName; } catch { }
                    if (!string.IsNullOrEmpty(exe))
                    {
                        try { icon = SD.Icon.ExtractAssociatedIcon(exe); } catch { }
                    }
                }
                catch { }

                // Filter out known invisible helper windows (WorkerW, shell
                // side windows) by class name.
                var cls = new StringBuilder(128);
                GetClassName(hwnd, cls, cls.Capacity);
                string className = cls.ToString();
                if (className is "WorkerW" or "Progman" or "Shell_TrayWnd"
                    or "Windows.UI.Core.CoreWindow" && string.IsNullOrEmpty(processName))
                {
                    return true;
                }

                results.Add(new WindowInfo(hwnd, title, processName, (int)pid,
                    bounds, minimized, icon));
            }
            catch
            {
                // Swallow per-window errors so a single bad window doesn't
                // break the whole enumeration.
            }
            return true;
        }, IntPtr.Zero);

        // EnumWindows yields top-down Z-order (topmost first); keep that order
        // so the picker shows active windows before background ones.
        return results;
    }

    /// <summary>
    /// Captures a single window's client frame (including border area) via
    /// <c>PrintWindow(PW_RENDERFULLCONTENT)</c>. Temporarily restores the
    /// window if it's minimized, then returns the bitmap on success or null
    /// if the OS refused the capture.
    /// </summary>
    public static SD.Bitmap? CaptureWindow(IntPtr hwnd, bool bringToFront = true)
    {
        if (hwnd == IntPtr.Zero) return null;

        bool wasIconic = IsIconic(hwnd);
        if (wasIconic)
        {
            ShowWindow(hwnd, SW_RESTORE);
            System.Threading.Thread.Sleep(120);
        }
        if (bringToFront) SetForegroundWindow(hwnd);

        if (!TryGetWindowBounds(hwnd, out var bounds) || bounds.Width <= 0 || bounds.Height <= 0)
            return null;

        // Primary path: PrintWindow renders the real pixels even for Chromium.
        var bmp = new SD.Bitmap(bounds.Width, bounds.Height, SDI.PixelFormat.Format32bppArgb);
        using (var g = SD.Graphics.FromImage(bmp))
        {
            g.Clear(SD.Color.Transparent);
            IntPtr hdc = g.GetHdc();
            try
            {
                bool ok = PrintWindow(hwnd, hdc, PW_RENDERFULLCONTENT);
                if (ok && !IsBitmapBlank(bmp))
                {
                    return bmp;
                }
            }
            finally
            {
                g.ReleaseHdc(hdc);
            }
        }
        bmp.Dispose();

        // Fallback: if PrintWindow came back blank (happens with some DWM
        // composited clients), grab the same rectangle off the screen itself.
        if (bringToFront)
        {
            System.Threading.Thread.Sleep(60);
            return ScreenCapture.CaptureScreenRegion(bounds);
        }
        return null;
    }

    public static bool TryGetWindowBounds(IntPtr hwnd, out SD.Rectangle bounds)
    {
        // Prefer the DWM-reported "extended frame" rect — that excludes the
        // invisible resize padding Windows adds around most top-level windows.
        if (DwmGetWindowAttribute(hwnd, DWMWA_EXTENDED_FRAME_BOUNDS, out RECT r, Marshal.SizeOf<RECT>()) == 0)
        {
            bounds = SD.Rectangle.FromLTRB(r.Left, r.Top, r.Right, r.Bottom);
            if (bounds.Width > 0 && bounds.Height > 0) return true;
        }
        if (GetWindowRect(hwnd, out var r2))
        {
            bounds = SD.Rectangle.FromLTRB(r2.Left, r2.Top, r2.Right, r2.Bottom);
            return bounds.Width > 0 && bounds.Height > 0;
        }
        bounds = SD.Rectangle.Empty;
        return false;
    }

    public static void BringToFront(IntPtr hwnd)
    {
        if (IsIconic(hwnd)) ShowWindow(hwnd, SW_RESTORE);
        SetForegroundWindow(hwnd);
    }

    /// <summary>Sample a sparse grid of pixels; if they're all transparent /
    /// pure black we assume PrintWindow failed and fall back to screen-grab.</summary>
    private static bool IsBitmapBlank(SD.Bitmap bmp)
    {
        int w = bmp.Width, h = bmp.Height;
        int step = Math.Max(8, Math.Min(w, h) / 16);
        int nonBlack = 0;
        int checks = 0;
        for (int y = step / 2; y < h; y += step)
        {
            for (int x = step / 2; x < w; x += step)
            {
                checks++;
                var c = bmp.GetPixel(x, y);
                if (c.A != 0 && (c.R | c.G | c.B) != 0)
                {
                    nonBlack++;
                    if (nonBlack >= 4) return false;
                }
            }
        }
        return checks > 0 && nonBlack == 0;
    }
}
