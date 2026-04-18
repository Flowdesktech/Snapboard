using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Snapboard.Helpers;
using Snapboard.Settings;
using SD = System.Drawing;

namespace Snapboard.ScrollingCapture;

/// <summary>
/// PicPick-style scrolling capture runner. Given a target window handle and
/// a scroll anchor point, this window auto-scrolls the target by synthesising
/// mouse-wheel input, captures each resulting frame, detects when no new
/// content appears, and stitches the frames into a single tall image which
/// is auto-saved to the user's Snapboard folder.
/// </summary>
public partial class ScrollingSessionWindow : Window
{
    // ---------------- Win32 ----------------

    [DllImport("user32.dll")]
    private static extern bool SetCursorPos(int x, int y);

    [DllImport("user32.dll")]
    private static extern bool GetCursorPos(out POINT lpPoint);

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr hwnd, int cmd);

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hwnd);

    [DllImport("user32.dll")]
    private static extern bool IsIconic(IntPtr hwnd);

    [DllImport("user32.dll")]
    private static extern void mouse_event(uint dwFlags, int dx, int dy, int dwData, IntPtr dwExtraInfo);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool PostMessage(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SendMessage(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern IntPtr WindowFromPoint(POINT point);

    [DllImport("user32.dll")]
    private static extern bool ScreenToClient(IntPtr hWnd, ref POINT lpPoint);

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT { public int X; public int Y; }

    private const uint MOUSEEVENTF_WHEEL = 0x0800;
    private const uint WM_MOUSEWHEEL = 0x020A;
    private const int  SW_RESTORE = 9;
    private const int  WHEEL_DELTA = 120;

    // ---------------- State ----------------

    /// <summary>Deepest content child to capture (e.g. Chrome's render
    /// widget). Its screen rect is what we crop each frame to, so the
    /// stitched image has no title bars, toolbars, or status bars.</summary>
    private readonly IntPtr _targetHwnd;

    /// <summary>Top-level window that owns the content child. Used for
    /// foreground activation (you can only <c>SetForegroundWindow</c> on
    /// top-level windows) and for the tray-toast "captured this window"
    /// label. For simple single-HWND apps it's the same as <see cref="_targetHwnd"/>.</summary>
    private readonly IntPtr _topHwnd;

    private readonly SD.Point _scrollAnchor;
    private readonly SD.Rectangle _initialWindowRect;
    private readonly List<SD.Bitmap> _frames = new();
    private DispatcherTimer? _timer;
    private ImageStitcher.LockedBitmap? _lastLocked;
    private bool _finished;
    private int _idleTicks;
    private int _maxTicks = 360;          // hard safety: ~3 minutes at 500ms
    private int _tickCount;
    private SD.Point _savedCursor;
    private bool _savedCursorCaptured;
    private bool _boosterSent;            // set while verifying "reached bottom" with a bigger scroll
    private List<Window> _hiddenDuringSession = new();
    private ScrollingTargetOutlineWindow? _outline;

    public ScrollingSessionWindow(
        IntPtr topHwnd,
        IntPtr captureHwnd,
        SD.Point scrollAnchor,
        SD.Rectangle initialCaptureRect)
    {
        _topHwnd = topHwnd;
        _targetHwnd = captureHwnd == IntPtr.Zero ? topHwnd : captureHwnd;
        _scrollAnchor = scrollAnchor;
        _initialWindowRect = initialCaptureRect;
        InitializeComponent();
    }

    // ---------------- Lifecycle ----------------

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        DarkTitleBar.Apply(this);
        // Critical: hide *this* window from every screen-capture call we
        // make below (CopyFromScreen, PrintWindow, etc.) so our own toolbar
        // never bleeds into the stitched output when it's positioned over
        // the target. Uses SetWindowDisplayAffinity(WDA_EXCLUDEFROMCAPTURE).
        // The global class handler in App.xaml.cs already applied this to
        // every Snapboard window; this call is defensive belt-and-braces.
        CaptureAffinity.ExcludeFromCapture(this);

        // On Win10 builds older than 2004, WDA_EXCLUDEFROMCAPTURE is a no-op
        // — so also physically hide the dashboard / settings / etc. during
        // the session. Everything except this session toolbar disappears
        // from the screen entirely, guaranteeing our own UI can never bleed
        // into the stitched output. They're restored on OnClosed.
        _hiddenDuringSession = ((App)Application.Current)
            .HideForCapture(typeof(ScrollingSessionWindow));

        // Draw a red outline around the target so the user always sees which
        // window is being captured. It follows the target if it moves and is
        // click-through + excluded from captures, so it never interferes.
        try
        {
            _outline = new ScrollingTargetOutlineWindow(_targetHwnd, _initialWindowRect);
            _outline.Show();
        }
        catch { /* outline is purely visual — never fail the session over it */ }

        StartRecordingDotPulse();
        PositionAwayFromWindow();

        // Tray toast so the user always gets an OS-level "capture started"
        // cue — important when the session toolbar is tucked into a corner
        // or partially covered by the target window. Mirrors the OCR and
        // window-capture flows which already announce themselves this way.
        try
        {
            ((App)Application.Current).NotifyInfo(
                "Scrolling capture started",
                "Auto-scrolling the selected content area and stitching frames. Click Stop & save to finish early.");
        }
        catch { /* toast is cosmetic — never fail the session over it */ }

        // Bring the target window up front once. We intentionally don't
        // re-steal focus on every tick — the ScrollingSession window uses
        // ShowActivated=false so the target keeps focus after our initial
        // SetForegroundWindow.
        EnsureTargetForeground();

        // Remember the user's cursor so we can restore it when we're done.
        if (GetCursorPos(out var pt))
        {
            _savedCursor = new SD.Point(pt.X, pt.Y);
            _savedCursorCaptured = true;
        }

        _timer = new DispatcherTimer(DispatcherPriority.Background)
        {
            // 500ms balances responsiveness with enough render time for
            // browsers/chromium to paint the newly scrolled region.
            Interval = TimeSpan.FromMilliseconds(500),
        };
        _timer.Tick += OnTick;

        // Grab the first frame immediately, then scroll + continue on a timer.
        Dispatcher.BeginInvoke(new Action(() =>
        {
            CaptureFrame(isFirst: true);
            UpdateStatus();
            SendWheelScroll(notches: 2);
            _timer.Start();
        }), DispatcherPriority.Background);
    }

    private void OnClosed(object? sender, EventArgs e)
    {
        _timer?.Stop();
        _lastLocked?.Dispose();
        if (!_finished)
        {
            foreach (var f in _frames) f.Dispose();
            _frames.Clear();
        }

        // Always put the cursor back where we found it so auto-scroll doesn't
        // leave the user's pointer parked over the captured window.
        if (_savedCursorCaptured)
        {
            try { SetCursorPos(_savedCursor.X, _savedCursor.Y); } catch { }
        }

        // Tear down the red target-outline overlay if it's still up.
        try { _outline?.Close(); } catch { }
        _outline = null;

        // Bring the Snapboard windows we hid for the session back.
        App.RestoreAfterCapture(_hiddenDuringSession);
        _hiddenDuringSession = new List<Window>();
    }

    private void OnWindowMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Left && e.LeftButton == MouseButtonState.Pressed)
        {
            try { DragMove(); } catch { }
        }
    }

    // ---------------- Positioning ----------------

    private void PositionAwayFromWindow()
    {
        UpdateLayout();
        double w = ActualWidth  > 0 ? ActualWidth  : 340;
        double h = ActualHeight > 0 ? ActualHeight : 140;

        var dpi = VisualTreeHelper.GetDpi(this);
        double rX = _initialWindowRect.Left   / dpi.DpiScaleX;
        double rY = _initialWindowRect.Top    / dpi.DpiScaleY;
        double rW = _initialWindowRect.Width  / dpi.DpiScaleX;
        double rH = _initialWindowRect.Height / dpi.DpiScaleY;

        var virt = System.Windows.Forms.SystemInformation.VirtualScreen;
        double vL = virt.Left   / dpi.DpiScaleX;
        double vT = virt.Top    / dpi.DpiScaleY;
        double vR = (virt.Left + virt.Width)  / dpi.DpiScaleX;
        double vB = (virt.Top  + virt.Height) / dpi.DpiScaleY;

        // Prefer above the target's title bar; otherwise right; otherwise below.
        double left = rX;
        double top  = rY - h - 12;
        if (top < vT + 10)
        {
            // Put it to the right of the target instead.
            left = rX + rW + 12;
            top  = rY + 12;
        }
        if (left + w > vR - 10)
        {
            left = rX; top = rY + rH + 12;
        }
        if (top + h > vB - 10) top = vB - h - 10;
        if (top < vT + 10)     top = vT + 10;
        if (left + w > vR - 10) left = vR - w - 10;
        if (left < vL + 10)     left = vL + 10;

        Left = left;
        Top  = top;
    }

    private void StartRecordingDotPulse()
    {
        var anim = new DoubleAnimation(1.0, 0.25, TimeSpan.FromMilliseconds(700))
        {
            AutoReverse = true,
            RepeatBehavior = RepeatBehavior.Forever,
        };
        RecDot.BeginAnimation(OpacityProperty, anim);
    }

    // ---------------- Auto-scroll + capture loop ----------------

    private void OnTick(object? sender, EventArgs e)
    {
        if (_finished) return;
        _tickCount++;
        if (_tickCount > _maxTicks)
        {
            Finish();
            return;
        }

        try
        {
            CaptureFrame(isFirst: false);
        }
        catch
        {
            // One bad tick shouldn't kill the session.
        }

        UpdateStatus();

        if (_finished) return;

        // Before giving up, fire a much bigger "booster" scroll to distinguish
        // "page is actually at the bottom" from "target briefly rendered the
        // same pixels" (lazy images, sticky headers, heavy layout). We only
        // declare finished after the booster *also* produces no new content.
        if (_idleTicks >= 2 && !_boosterSent)
        {
            _boosterSent = true;
            SendWheelScroll(notches: 10); // roughly a PageDown worth of wheel
            return;
        }

        // If we've seen no new content even after the booster, the page is
        // done — stop sending scroll events and finalize.
        if (_idleTicks >= 5)
        {
            Finish();
            return;
        }

        SendWheelScroll(notches: 2);
    }

    private void CaptureFrame(bool isFirst)
    {
        // Refresh the bounds each tick: some apps (e.g. modern Edge) resize
        // their client area mid-scroll (sticky header hiding). We still use
        // the initial bounds as the anchor so dimensions stay stable across
        // frames — if they change mid-way, we clip-snap back to match.
        SD.Rectangle bounds = _initialWindowRect;
        if (WindowEnumerator.TryGetWindowBounds(_targetHwnd, out var cur))
        {
            if (cur.Width == bounds.Width && cur.Height == bounds.Height)
                bounds = cur; // window moved; follow it
        }

        SD.Bitmap shot = ScreenCapture.CaptureScreenRegion(bounds);

        // First frame: keep unconditionally.
        if (isFirst || _frames.Count == 0)
        {
            _frames.Add(shot);
            _lastLocked?.Dispose();
            _lastLocked = ImageStitcher.LockedBitmap.Lock(shot);
            _idleTicks = 0;
            return;
        }

        // Ensure dimensions match — stitcher requires identical sizes.
        if (shot.Width != _frames[0].Width || shot.Height != _frames[0].Height)
        {
            // Window resized under us. Stop and stitch what we have.
            shot.Dispose();
            Finish();
            return;
        }

        using var curLocked = ImageStitcher.LockedBitmap.Lock(shot);

        // Idle detection is done with a grid-sampling pixel-diff check that
        // looks at the *whole* frame (not just a top strip). This is way
        // more reliable than trying to overload FindBestOverlap — on pages
        // with lots of whitespace/uniform background, the overlap search can
        // latch onto a spurious near-max match and incorrectly flag every
        // new-content frame as "identical", which was the 0.1.0 bug where
        // only the first frame was ever kept. Vertical alignment is still
        // done later, at stitch time, by FindBestOverlap.
        if (ImageStitcher.AreFramesNearIdentical(_lastLocked!, curLocked))
        {
            shot.Dispose();
            _idleTicks++;
            return;
        }

        _frames.Add(shot);
        _lastLocked?.Dispose();
        _lastLocked = ImageStitcher.LockedBitmap.Lock(shot);
        _idleTicks = 0;
        _boosterSent = false; // new content arrived: re-enable the booster verify
    }

    /// <summary>
    /// Synthesises a mouse-wheel scroll at the anchor point. We prefer posting
    /// <c>WM_MOUSEWHEEL</c> directly to the deepest child window under the
    /// anchor — this is what PicPick / ShareX-style scroll tools do because
    /// it works reliably even when the target isn't in the foreground, and
    /// targets the correct render widget in Chromium apps (Chrome, Edge,
    /// Electron). We also fire a classic <c>mouse_event</c> path as a
    /// fallback for native controls that ignore posted messages.
    /// </summary>
    private void SendWheelScroll(int notches = 2)
    {
        if (notches <= 0) notches = 1;
        int delta = -WHEEL_DELTA * notches;

        try
        {
            // 1) Message-based scroll: target the real child under the anchor.
            IntPtr target = _targetHwnd;
            try
            {
                var pt = new POINT { X = _scrollAnchor.X, Y = _scrollAnchor.Y };
                IntPtr hit = WindowFromPoint(pt);
                if (hit != IntPtr.Zero) target = hit;
            }
            catch { /* keep top-level handle */ }

            // WM_MOUSEWHEEL: wParam high word = delta, low word = key flags;
            // lParam = MAKELPARAM(screenX, screenY) (NB: screen coords, not
            // client coords — that's how wheel messages work).
            int wParamHi = delta;
            int wParamLo = 0;
            IntPtr wParam = (IntPtr)((wParamHi << 16) | (wParamLo & 0xFFFF));
            IntPtr lParam = (IntPtr)((_scrollAnchor.Y << 16) | (_scrollAnchor.X & 0xFFFF));

            PostMessage(target, WM_MOUSEWHEEL, wParam, lParam);
        }
        catch { }

        try
        {
            // 2) Fallback: real hardware wheel at the anchor. Some legacy
            //    Win32 controls (older Delphi apps, some installers) only
            //    respond to actual input, not to synthesized WM_MOUSEWHEEL.
            SetCursorPos(_scrollAnchor.X, _scrollAnchor.Y);
            mouse_event(MOUSEEVENTF_WHEEL, 0, 0, delta, IntPtr.Zero);
        }
        catch { }
    }

    private void EnsureTargetForeground()
    {
        // SetForegroundWindow only accepts top-level windows — passing
        // a child HWND (e.g. Chrome_RenderWidgetHostHWND) is a no-op.
        try
        {
            if (IsIconic(_topHwnd)) ShowWindow(_topHwnd, SW_RESTORE);
            SetForegroundWindow(_topHwnd);
        }
        catch { }
    }

    private void UpdateStatus()
    {
        FrameCountText.Text = _frames.Count == 1 ? "1 frame" : $"{_frames.Count} frames";
        if (_idleTicks > 0)
            StatusText.Text = _idleTicks >= 2 ? "Finishing — no new content." : "Waiting for new content…";
        else
            StatusText.Text = "Auto-scrolling target window…";
    }

    // ---------------- Actions ----------------

    private void OnStopClick(object sender, RoutedEventArgs e) => Finish();
    private void OnCancelClick(object sender, RoutedEventArgs e) => Cancel();

    private void Cancel()
    {
        _finished = true;
        _timer?.Stop();
        foreach (var f in _frames) f.Dispose();
        _frames.Clear();
        Close();
    }

    private void Finish()
    {
        if (_finished) return;
        _finished = true;
        _timer?.Stop();
        _lastLocked?.Dispose();
        _lastLocked = null;

        if (_frames.Count == 0)
        {
            Close();
            return;
        }

        // Take ownership of the frames so OnClosed doesn't dispose them.
        var frames = _frames.ToArray();
        _frames.Clear();

        StatusText.Text = "Stitching frames…";
        FrameCountText.Text = frames.Length == 1 ? "1 frame" : $"{frames.Length} frames";

        _ = System.Threading.Tasks.Task.Run(() =>
        {
            SD.Bitmap? stitched = null;
            string? error = null;
            try
            {
                stitched = ImageStitcher.Stitch(frames);
            }
            catch (Exception ex) { error = ex.Message; }
            finally
            {
                foreach (var f in frames) f.Dispose();
            }

            Dispatcher.Invoke(() =>
            {
                try
                {
                    var app = (App)Application.Current;
                    if (error != null || stitched == null)
                    {
                        app.NotifyInfo("Scrolling capture failed",
                            error ?? "Could not stitch frames.",
                            System.Windows.Forms.ToolTipIcon.Error);
                        return;
                    }

                    HandleStitchedResult(stitched, app);
                }
                finally
                {
                    stitched?.Dispose();
                    Close();
                }
            });
        });
    }

    /// <summary>
    /// Post-capture flow (UI thread): copy the stitched image to the
    /// clipboard, prompt the user for a save destination, persist the file
    /// if confirmed, and surface a single summarising tray toast. Mirrors the
    /// "Capture window" workflow so both capture modes feel identical.
    /// </summary>
    private void HandleStitchedResult(SD.Bitmap stitched, App app)
    {
        BitmapSource? src = null;
        bool copied = false;

        try
        {
            src = ScreenCapture.ToBitmapSource(stitched) as BitmapSource;
            if (src != null)
            {
                try { System.Windows.Clipboard.SetImage(src); copied = true; }
                catch { /* clipboard can be momentarily locked */ }
            }
        }
        catch
        {
            // Fall through — we still try to save even if clipboard conversion
            // failed.
        }

        string? savedPath = null;
        string? saveError = null;
        try { savedPath = PromptSave(stitched, app); }
        catch (Exception ex) { saveError = ex.Message; }

        // Single toast summarising both operations.
        if (savedPath != null && copied)
        {
            app.NotifyInfo("Scrolling capture saved",
                $"Stitched image ({stitched.Width}×{stitched.Height}) copied to clipboard and saved to {Path.GetFileName(savedPath)}.");
        }
        else if (savedPath != null)
        {
            app.NotifyInfo("Scrolling capture saved",
                $"Stitched image ({stitched.Width}×{stitched.Height}) saved to {Path.GetFileName(savedPath)}.");
        }
        else if (copied)
        {
            app.NotifyInfo("Scrolling capture",
                $"Stitched image ({stitched.Width}×{stitched.Height}) copied to clipboard.");
        }
        else if (saveError != null)
        {
            app.NotifyInfo("Scrolling capture",
                "Captured but failed to save: " + saveError,
                System.Windows.Forms.ToolTipIcon.Error);
        }
    }

    /// <summary>
    /// Prompts the user for a save destination (respecting configured default
    /// format, JPEG quality, and save directory) and writes the stitched
    /// image to disk. Returns the saved path or <c>null</c> if the user
    /// cancelled the dialog.
    /// </summary>
    private string? PromptSave(SD.Bitmap stitched, App app)
    {
        var settings = app.Settings;
        bool isJpeg = settings.DefaultFormat.Equals("jpg", StringComparison.OrdinalIgnoreCase)
                   || settings.DefaultFormat.Equals("jpeg", StringComparison.OrdinalIgnoreCase);
        string ext = isJpeg ? ".jpg" : ".png";
        string fileName = "Snapboard-scroll-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ext;

        string defaultDir;
        try { defaultDir = SettingsService.ResolveSaveDirectory(settings); }
        catch
        {
            defaultDir = string.IsNullOrWhiteSpace(settings.SaveDirectory)
                ? Environment.GetFolderPath(Environment.SpecialFolder.MyPictures)
                : settings.SaveDirectory;
        }

        var dlg = new Microsoft.Win32.SaveFileDialog
        {
            Title = "Save scrolling capture",
            Filter = isJpeg
                ? "JPEG image (*.jpg)|*.jpg|PNG image (*.png)|*.png"
                : "PNG image (*.png)|*.png|JPEG image (*.jpg)|*.jpg",
            FileName = fileName,
            InitialDirectory = defaultDir,
            AddExtension = true,
            OverwritePrompt = true,
        };

        // Showing the dialog owned by this session window keeps it parented
        // correctly and honours our dark-title-bar setup.
        bool? ok = IsLoaded && IsVisible ? dlg.ShowDialog(this) : dlg.ShowDialog();
        if (ok != true) return null;

        Directory.CreateDirectory(Path.GetDirectoryName(dlg.FileName)!);
        BitmapSaver.Save(stitched, dlg.FileName, settings.JpegQuality);
        return dlg.FileName;
    }
}
