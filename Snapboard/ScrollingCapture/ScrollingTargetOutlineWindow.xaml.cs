using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using Snapboard.Helpers;
using SD = System.Drawing;

namespace Snapboard.ScrollingCapture;

/// <summary>
/// A thin, click-through, always-on-top window that draws a red outline
/// around the scroll target while <see cref="ScrollingSessionWindow"/> is
/// running. Purely visual feedback — Snapboard is excluded from captures
/// via <c>WDA_EXCLUDEFROMCAPTURE</c>, so this overlay never appears in the
/// stitched output. Tracks the target window's position every tick so the
/// outline follows windows that are moved or resized mid-capture.
/// </summary>
public partial class ScrollingTargetOutlineWindow : Window
{
    [DllImport("user32.dll")]
    private static extern bool GetWindowRect(IntPtr hwnd, out RECT rect);

    [DllImport("user32.dll", EntryPoint = "GetWindowLong")]
    private static extern int GetWindowLong32(IntPtr hwnd, int nIndex);

    [DllImport("user32.dll", EntryPoint = "SetWindowLong")]
    private static extern int SetWindowLong32(IntPtr hwnd, int nIndex, int dwNewLong);

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT { public int Left, Top, Right, Bottom; }

    private const int GWL_EXSTYLE        = -20;
    private const int WS_EX_TRANSPARENT  = 0x00000020;
    private const int WS_EX_TOOLWINDOW   = 0x00000080;
    private const int WS_EX_NOACTIVATE   = 0x08000000;

    private readonly IntPtr _target;
    private DispatcherTimer? _followTimer;

    public ScrollingTargetOutlineWindow(IntPtr target, SD.Rectangle initial)
    {
        _target = target;
        InitializeComponent();
        ApplyRect(initial);
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        // Click-through + no taskbar activation. This has to be set after
        // the HWND exists, which is why we hook it in Loaded rather than
        // in the ctor.
        var hwnd = new WindowInteropHelper(this).Handle;
        if (hwnd != IntPtr.Zero)
        {
            int ex = GetWindowLong32(hwnd, GWL_EXSTYLE);
            SetWindowLong32(hwnd, GWL_EXSTYLE,
                ex | WS_EX_TRANSPARENT | WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE);
        }

        // Hide this red outline from every screen-capture call — it's
        // purely visual feedback for the user and must not end up in the
        // stitched output. No-op on Win10 < 2004.
        CaptureAffinity.ExcludeFromCapture(this);

        // Follow the target so the outline tracks window moves/resizes.
        // 100ms is fast enough to feel live without hammering the CPU.
        _followTimer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromMilliseconds(100),
        };
        _followTimer.Tick += (_, _) =>
        {
            if (GetWindowRect(_target, out var r))
            {
                ApplyRect(SD.Rectangle.FromLTRB(r.Left, r.Top, r.Right, r.Bottom));
            }
        };
        _followTimer.Start();
    }

    private void ApplyRect(SD.Rectangle screenRect)
    {
        var dpi = VisualTreeHelper.GetDpi(this);
        Left   = screenRect.Left   / dpi.DpiScaleX;
        Top    = screenRect.Top    / dpi.DpiScaleY;
        Width  = Math.Max(20, screenRect.Width  / dpi.DpiScaleX);
        Height = Math.Max(20, screenRect.Height / dpi.DpiScaleY);
    }

    protected override void OnClosed(EventArgs e)
    {
        _followTimer?.Stop();
        _followTimer = null;
        base.OnClosed(e);
    }
}
