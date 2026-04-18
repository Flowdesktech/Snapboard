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
    private static extern IntPtr GetAncestor(IntPtr hwnd, uint gaFlags);

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern int GetWindowText(IntPtr hwnd, StringBuilder lpString, int nMaxCount);

    [DllImport("user32.dll")]
    private static extern int GetWindowTextLength(IntPtr hwnd);

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

    // ----------------------------- State -----------------------------

    /// <summary>Top-level window the user clicked, or null on cancel.</summary>
    public IntPtr PickedHwnd { get; private set; } = IntPtr.Zero;

    /// <summary>Screen coordinates of the click, useful for targeting the
    /// scroll-wheel input at a specific scrollable child.</summary>
    public SD.Point ClickScreenPoint { get; private set; }

    /// <summary>Screen rect of the picked window (extended frame bounds).</summary>
    public SD.Rectangle PickedScreenRect { get; private set; }

    private IntPtr _selfHandle;
    private IntPtr _lastHovered;

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

        IntPtr target = ResolveTargetAt(sx, sy);
        if (target == IntPtr.Zero)
        {
            HoverBorder.Visibility = Visibility.Collapsed;
            HoverLabel.Visibility = Visibility.Collapsed;
            _lastHovered = IntPtr.Zero;
            return;
        }

        if (target != _lastHovered)
        {
            _lastHovered = target;
            UpdateHoverVisualForWindow(target, dpi);
        }

        PositionHoverLabel(dip);
    }

    private IntPtr ResolveTargetAt(int screenX, int screenY)
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

        IntPtr hwnd;
        try
        {
            hwnd = WindowFromPoint(new POINT { X = screenX, Y = screenY });
        }
        finally
        {
            if (toggled) SetWindowLong32(_selfHandle, GWL_EXSTYLE, prevEx);
        }

        if (hwnd == IntPtr.Zero) return IntPtr.Zero;
        hwnd = GetAncestor(hwnd, GA_ROOT);
        if (hwnd == IntPtr.Zero) return IntPtr.Zero;
        if (hwnd == _selfHandle) return IntPtr.Zero;
        if (hwnd == GetShellWindow()) return IntPtr.Zero;
        return hwnd;
    }

    private void UpdateHoverVisualForWindow(IntPtr hwnd, DpiScale dpi)
    {
        if (!GetWindowRect(hwnd, out var r)) { HoverBorder.Visibility = Visibility.Collapsed; return; }

        // Map screen rect → overlay-local DIPs.
        double x = r.Left  / dpi.DpiScaleX - Left;
        double y = r.Top   / dpi.DpiScaleY - Top;
        double w = (r.Right  - r.Left) / dpi.DpiScaleX;
        double h = (r.Bottom - r.Top)  / dpi.DpiScaleY;

        HoverBorder.Margin = new Thickness(x, y, 0, 0);
        HoverBorder.Width  = Math.Max(0, w);
        HoverBorder.Height = Math.Max(0, h);
        HoverBorder.Visibility = Visibility.Visible;

        int len = GetWindowTextLength(hwnd);
        var sb = new StringBuilder(Math.Max(32, len + 1));
        GetWindowText(hwnd, sb, sb.Capacity);
        string title = sb.ToString();
        if (string.IsNullOrWhiteSpace(title)) title = "(untitled window)";

        HoverLabelText.Text = title;
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

        IntPtr target = ResolveTargetAt(sx, sy);
        if (target == IntPtr.Zero)
        {
            // Nothing useful under the cursor — ignore and let the user try again.
            return;
        }

        PickedHwnd = target;
        ClickScreenPoint = new SD.Point(sx, sy);
        if (GetWindowRect(target, out var r))
        {
            PickedScreenRect = SD.Rectangle.FromLTRB(r.Left, r.Top, r.Right, r.Bottom);
        }
        Close();
    }
}
