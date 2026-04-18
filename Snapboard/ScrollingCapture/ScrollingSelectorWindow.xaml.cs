using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using SD = System.Drawing;

namespace Snapboard.ScrollingCapture;

/// <summary>
/// Fullscreen click-picker for scrolling capture (PicPick-style). The user
/// clicks once over the scrollable window they want to capture; we resolve
/// the top-level <c>HWND</c> at that screen point and hand it off to
/// <see cref="ScrollingSessionWindow"/> which auto-scrolls + stitches.
/// </summary>
public partial class ScrollingSelectorWindow : Window
{
    // ----------------------------- Win32 -----------------------------

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT { public int X; public int Y; }

    [DllImport("user32.dll")]
    private static extern IntPtr WindowFromPoint(POINT pt);

    [DllImport("user32.dll")]
    private static extern IntPtr ChildWindowFromPointEx(IntPtr hwndParent, POINT pt, uint flags);

    [DllImport("user32.dll")]
    private static extern IntPtr GetAncestor(IntPtr hwnd, uint gaFlags);

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern int GetWindowText(IntPtr hwnd, StringBuilder lpString, int nMaxCount);

    [DllImport("user32.dll")]
    private static extern int GetWindowTextLength(IntPtr hwnd);

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern int GetClassName(IntPtr hwnd, StringBuilder lpClassName, int nMaxCount);

    [DllImport("user32.dll")]
    private static extern bool GetWindowRect(IntPtr hwnd, out RECT rect);

    [DllImport("user32.dll")]
    private static extern IntPtr GetShellWindow();

    [DllImport("user32.dll", EntryPoint = "GetWindowLong")]
    private static extern int GetWindowLong32(IntPtr hwnd, int nIndex);

    [DllImport("user32.dll", EntryPoint = "SetWindowLong")]
    private static extern int SetWindowLong32(IntPtr hwnd, int nIndex, int dwNewLong);

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT { public int Left, Top, Right, Bottom; }

    private const uint GA_ROOT = 2;
    private const int GWL_EXSTYLE = -20;
    private const int WS_EX_TRANSPARENT = 0x00000020;

    private const uint CWP_SKIPINVISIBLE = 0x0001;
    private const uint CWP_SKIPTRANSPARENT = 0x0004;

    /// <summary>Window rects smaller than this (in screen pixels on either
    /// axis) are treated as "user probably hit a title-bar or scroll-bar
    /// control" and we fall back to the top-level window instead.</summary>
    private const int MinChildSize = 120;

    // ----------------------------- State -----------------------------

    /// <summary>Top-level window the user clicked, or <see cref="IntPtr.Zero"/>
    /// on cancel. Kept for context (tray toast titles, target-outline tracking)
    /// but not used as the capture region — we capture the inner child instead.</summary>
    public IntPtr PickedHwnd { get; private set; } = IntPtr.Zero;

    /// <summary>The deepest child window under the click — typically the
    /// scrollable content area (Chrome render widget, Scintilla, RichEdit,
    /// etc.) so the stitched output excludes title bars and toolbars. Falls
    /// back to <see cref="PickedHwnd"/> when no suitable child exists.</summary>
    public IntPtr CaptureHwnd { get; private set; } = IntPtr.Zero;

    /// <summary>Screen coordinates of the click, used as the anchor for the
    /// synthesized <c>WM_MOUSEWHEEL</c> events during auto-scroll.</summary>
    public SD.Point ClickScreenPoint { get; private set; }

    /// <summary>Screen rect of the picked <em>capture</em> window — i.e.
    /// the content child, not the top-level frame. This is the rectangle
    /// each auto-scrolled frame is cropped to before stitching.</summary>
    public SD.Rectangle PickedScreenRect { get; private set; }

    private IntPtr _selfHandle;
    private IntPtr _lastHoveredCapture;

    public ScrollingSelectorWindow()
    {
        InitializeComponent();

        // Span the entire virtual desktop so clicks on any monitor work.
        var virt = System.Windows.Forms.SystemInformation.VirtualScreen;
        WindowStartupLocation = WindowStartupLocation.Manual;
        Left   = virt.Left;
        Top    = virt.Top;
        Width  = virt.Width;
        Height = virt.Height;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        _selfHandle = new WindowInteropHelper(this).Handle;
        Focus();
    }

    private void OnKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            PickedHwnd = IntPtr.Zero;
            Close();
            e.Handled = true;
        }
    }

    // ------------------------ Hover preview -----------------------

    private void OnMouseMove(object sender, MouseEventArgs e)
    {
        // Convert the WPF mouse point to screen pixels.
        var dip = e.GetPosition(this);
        var dpi = VisualTreeHelper.GetDpi(this);
        int sx = (int)Math.Round(Left * dpi.DpiScaleX + dip.X * dpi.DpiScaleX);
        int sy = (int)Math.Round(Top  * dpi.DpiScaleY + dip.Y * dpi.DpiScaleY);

        var target = ResolveTargetAt(sx, sy);
        if (target.CaptureHwnd == IntPtr.Zero)
        {
            HoverBorder.Visibility = Visibility.Collapsed;
            HoverLabel.Visibility = Visibility.Collapsed;
            _lastHoveredCapture = IntPtr.Zero;
            return;
        }

        if (target.CaptureHwnd != _lastHoveredCapture)
        {
            _lastHoveredCapture = target.CaptureHwnd;
            UpdateHoverVisualForTarget(target, dpi);
        }

        PositionHoverLabel(dip);
    }

    /// <summary>Resolved pair of handles + rect for a screen point. The
    /// <see cref="TopHwnd"/> is the top-level window (used for window-title
    /// labels and foreground activation), while <see cref="CaptureHwnd"/> is
    /// the deepest content child — Chrome's render widget, a Scintilla
    /// editor, a RichEdit, a WebView2 host, etc. — which is what we actually
    /// crop frames to so the stitched output excludes title bars and
    /// toolbars. Both are zero on failure.</summary>
    private readonly struct ResolvedTarget
    {
        public readonly IntPtr TopHwnd;
        public readonly IntPtr CaptureHwnd;
        public readonly SD.Rectangle CaptureRect;
        public ResolvedTarget(IntPtr top, IntPtr capture, SD.Rectangle rect)
        { TopHwnd = top; CaptureHwnd = capture; CaptureRect = rect; }
        public static readonly ResolvedTarget Empty = default;
    }

    private ResolvedTarget ResolveTargetAt(int screenX, int screenY)
    {
        // Temporarily mark our overlay as click-through so WindowFromPoint
        // returns the window underneath instead of our own handle. We toggle
        // it back immediately so the overlay keeps receiving WPF events.
        int prevEx = 0;
        bool toggled = false;
        if (_selfHandle != IntPtr.Zero)
        {
            prevEx = GetWindowLong32(_selfHandle, GWL_EXSTYLE);
            if ((prevEx & WS_EX_TRANSPARENT) == 0)
            {
                SetWindowLong32(_selfHandle, GWL_EXSTYLE, prevEx | WS_EX_TRANSPARENT);
                toggled = true;
            }
        }

        IntPtr deepest;
        try
        {
            deepest = WindowFromPoint(new POINT { X = screenX, Y = screenY });
        }
        finally
        {
            if (toggled) SetWindowLong32(_selfHandle, GWL_EXSTYLE, prevEx);
        }

        if (deepest == IntPtr.Zero) return ResolvedTarget.Empty;

        IntPtr top = GetAncestor(deepest, GA_ROOT);
        if (top == IntPtr.Zero) return ResolvedTarget.Empty;
        if (top == _selfHandle) return ResolvedTarget.Empty;
        if (top == GetShellWindow()) return ResolvedTarget.Empty;

        // Don't let the user accidentally target Snapboard's own windows
        // (dashboard, settings, about, toasts, …). Otherwise we'd end up
        // "scrolling" our own UI and every stitched frame would be a
        // screenshot of Snapboard instead of the user's target.
        if (IsOwnWindow(top)) return ResolvedTarget.Empty;

        // Pick the best capture child: the deepest visible window under the
        // cursor that's large enough to contain "real" scrollable content.
        // This is the PicPick trick — for Chrome / Edge / Electron we land on
        // the render widget child which is exactly the page area (no tabs,
        // no omnibox, no status bar), so the stitched output is clean.
        IntPtr captureHwnd = PickBestCaptureChild(top, deepest, screenX, screenY);
        if (captureHwnd == IntPtr.Zero || !GetWindowRect(captureHwnd, out var cr))
        {
            if (!GetWindowRect(top, out cr)) return ResolvedTarget.Empty;
            captureHwnd = top;
        }

        var rect = SD.Rectangle.FromLTRB(cr.Left, cr.Top, cr.Right, cr.Bottom);

        // If the resolved child is implausibly tiny on either axis (user
        // hovered over a scroll-bar or a title-bar control) fall back to
        // the top-level window rect — a slightly chromed capture is way
        // better than capturing a 20-pixel-tall slice.
        if (rect.Width < MinChildSize || rect.Height < MinChildSize)
        {
            if (GetWindowRect(top, out var tr))
            {
                rect = SD.Rectangle.FromLTRB(tr.Left, tr.Top, tr.Right, tr.Bottom);
                captureHwnd = top;
            }
        }

        return new ResolvedTarget(top, captureHwnd, rect);
    }

    /// <summary>
    /// Walks down from <paramref name="deepest"/> (what
    /// <c>WindowFromPoint</c> returned) toward <paramref name="top"/>,
    /// picking the first ancestor that's at least <see cref="MinChildSize"/>
    /// in both directions. This handles Chrome's multi-layered HWND tree
    /// (<c>Chrome_RenderWidgetHostHWND</c> inside <c>Chrome_WidgetWin_1</c>),
    /// Electron apps, and simple single-HWND apps alike.
    /// </summary>
    private static IntPtr PickBestCaptureChild(IntPtr top, IntPtr deepest, int sx, int sy)
    {
        if (deepest == IntPtr.Zero || deepest == top) return top;

        // Prefer the deepest child itself if it has a generous rect.
        if (GetWindowRect(deepest, out var dr))
        {
            int dw = dr.Right - dr.Left;
            int dh = dr.Bottom - dr.Top;
            if (dw >= MinChildSize && dh >= MinChildSize) return deepest;
        }

        // Otherwise walk the ancestor chain toward top, stopping at the
        // first reasonably sized one.
        IntPtr cur = deepest;
        for (int i = 0; i < 16 && cur != IntPtr.Zero && cur != top; i++)
        {
            IntPtr parent = GetAncestor(cur, 1 /* GA_PARENT */);
            if (parent == IntPtr.Zero || parent == top) break;
            if (GetWindowRect(parent, out var pr))
            {
                int pw = pr.Right - pr.Left;
                int ph = pr.Bottom - pr.Top;
                if (pw >= MinChildSize && ph >= MinChildSize) return parent;
            }
            cur = parent;
        }

        return top;
    }

    private static bool IsOwnWindow(IntPtr hwnd)
    {
        foreach (Window w in Application.Current.Windows)
        {
            var h = new WindowInteropHelper(w).Handle;
            if (h != IntPtr.Zero && h == hwnd) return true;
        }
        return false;
    }

    private void UpdateHoverVisualForTarget(ResolvedTarget t, DpiScale dpi)
    {
        var r = t.CaptureRect;

        // Map screen rect → overlay-local DIPs.
        double x = r.Left  / dpi.DpiScaleX - Left;
        double y = r.Top   / dpi.DpiScaleY - Top;
        double w = r.Width  / dpi.DpiScaleX;
        double h = r.Height / dpi.DpiScaleY;

        HoverBorder.Margin = new Thickness(x, y, 0, 0);
        HoverBorder.Width  = Math.Max(0, w);
        HoverBorder.Height = Math.Max(0, h);
        HoverBorder.Visibility = Visibility.Visible;

        // Label: top-level title + a hint when we've picked an inner child
        // (so the user understands why the red rectangle doesn't match the
        // full window frame).
        int len = GetWindowTextLength(t.TopHwnd);
        var sb = new StringBuilder(Math.Max(32, len + 1));
        GetWindowText(t.TopHwnd, sb, sb.Capacity);
        string title = sb.ToString();
        if (string.IsNullOrWhiteSpace(title)) title = "(untitled window)";

        if (t.CaptureHwnd != t.TopHwnd)
        {
            HoverLabelText.Text = title + "  —  content area";
        }
        else
        {
            HoverLabelText.Text = title;
        }
        HoverLabel.Visibility = Visibility.Visible;
    }

    private void PositionHoverLabel(Point mouseDip)
    {
        HoverLabel.UpdateLayout();
        double lx = mouseDip.X + 18;
        double ly = mouseDip.Y + 18;
        if (lx + HoverLabel.ActualWidth  > ActualWidth - 8)
            lx = mouseDip.X - HoverLabel.ActualWidth - 18;
        if (ly + HoverLabel.ActualHeight > ActualHeight - 8)
            ly = mouseDip.Y - HoverLabel.ActualHeight - 18;
        HoverLabel.Margin = new Thickness(Math.Max(4, lx), Math.Max(4, ly), 0, 0);
    }

    // ---------------------------- Click ---------------------------

    private void OnMouseDown(object sender, MouseButtonEventArgs e)
    {
        var dip = e.GetPosition(this);
        var dpi = VisualTreeHelper.GetDpi(this);
        int sx = (int)Math.Round(Left * dpi.DpiScaleX + dip.X * dpi.DpiScaleX);
        int sy = (int)Math.Round(Top  * dpi.DpiScaleY + dip.Y * dpi.DpiScaleY);

        var target = ResolveTargetAt(sx, sy);
        if (target.CaptureHwnd == IntPtr.Zero)
        {
            // Nothing useful under the cursor — ignore and let the user try again.
            return;
        }

        PickedHwnd = target.TopHwnd;
        CaptureHwnd = target.CaptureHwnd;
        ClickScreenPoint = new SD.Point(sx, sy);
        PickedScreenRect = target.CaptureRect;
        Close();
    }
}
