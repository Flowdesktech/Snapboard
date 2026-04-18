using System.Drawing;
using System.IO;
using System.Windows;
using System.Windows.Media.Imaging;
using System.Windows.Resources;
using Snapboard.ColorPicker;
using Snapboard.Helpers;
using Snapboard.Ocr;
using Snapboard.Ruler;
using Snapboard.ScrollingCapture;
using Snapboard.Settings;
using WF = System.Windows.Forms;

namespace Snapboard;

public partial class App : Application
{
    private WF.NotifyIcon? _tray;
    private HotkeyManager? _hotkey;
    private bool _balloonShown;
    private bool _startupHotkeyAlertShown;

    /// <summary>True when launched with the <c>--autostart</c> flag (i.e. by
    /// the Windows logon Run key). Causes the dashboard window to stay hidden
    /// on boot so the app silently takes up residence in the tray.</summary>
    public bool LaunchedAsAutoStart { get; private set; }

    public AppSettings Settings { get; private set; } = new();
    public bool CaptureHotkeyRegistered { get; private set; }
    public bool FullScreenHotkeyRegistered { get; private set; }
    public bool ColorPickerHotkeyRegistered { get; private set; }
    public bool OcrHotkeyRegistered { get; private set; }
    public bool PixelRulerHotkeyRegistered { get; private set; }
    public bool IsShuttingDown { get; private set; }

    /// <summary>A hotkey that couldn't be bound — usually because another app owns it.</summary>
    public record HotkeyFailure(string Name, string Hotkey);

    /// <summary>Populated by the most recent <see cref="ApplyHotkeys"/> call.</summary>
    public IReadOnlyList<HotkeyFailure> HotkeyFailures { get; private set; } = Array.Empty<HotkeyFailure>();

    private void Application_Startup(object sender, StartupEventArgs e)
    {
        LaunchedAsAutoStart = e.Args.Any(a =>
            string.Equals(a, StartupRegistration.AutoStartArg, StringComparison.OrdinalIgnoreCase));

        Settings = SettingsService.Load();

        // Keep the Run-key entry in sync with the saved preference every time
        // the app starts — handles cases where the exe was moved/updated and
        // the registered command line no longer points at the current binary.
        StartupRegistration.Apply(Settings.RunOnStartup);

        _tray = new WF.NotifyIcon
        {
            Icon = LoadAppIcon() ?? SystemIcons.Application,
            Visible = true,
            Text = "Snapboard — left-click to capture",
        };
        BuildTrayMenu();
        _tray.MouseClick += OnTrayMouseClick;

        _hotkey = new HotkeyManager();
        ApplyHotkeys();

        Dispatcher.BeginInvoke(new Action(() =>
        {
            if (MainWindow is MainWindow mw)
            {
                mw.RefreshFromSettings();

                // Auto-started runs should boot silently to the tray — hide
                // the dashboard immediately so it doesn't flash on login.
                if (LaunchedAsAutoStart)
                {
                    mw.Hide();
                    ShowTrayBalloonOnce(
                        "Snapboard is running",
                        "Press your Capture hotkey or click the tray icon to start a capture.");
                }
            }
            NotifyHotkeyFailuresIfAny();
            ShowStartupHotkeyFailureAlertIfAny();
        }));
    }

    private void NotifyHotkeyFailuresIfAny()
    {
        if (HotkeyFailures.Count == 0) return;
        NotifyTray(
            "Some hotkeys couldn't be registered",
            $"{FormatFailures(HotkeyFailures)}\n\nLikely already in use by another app. Change them in Settings.",
            WF.ToolTipIcon.Warning);
    }

    /// <summary>
    /// Visible startup alert for hotkey registration failures.
    /// We keep tray toasts too, but this message box ensures users notice
    /// conflicts right away on regular launches (non-autostart runs).
    /// </summary>
    private void ShowStartupHotkeyFailureAlertIfAny()
    {
        if (_startupHotkeyAlertShown) return;
        if (LaunchedAsAutoStart) return; // avoid blocking silent tray boot
        if (HotkeyFailures.Count == 0) return;

        _startupHotkeyAlertShown = true;
        try
        {
            MessageBox.Show(
                MainWindow,
                "Some hotkeys could not be registered:\n\n" +
                $"{FormatFailures(HotkeyFailures)}\n\n" +
                "Those combinations are likely already used by another app. " +
                "Open Settings and choose different hotkeys.",
                "Snapboard — hotkey registration failed",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
        catch
        {
            // Best-effort alert. If a modal cannot be shown for any reason,
            // the tray warning + in-app status still communicate the failure.
        }
    }

    // ---------------- Tray ----------------

    private void BuildTrayMenu()
    {
        if (_tray == null) return;
        var menu = new WF.ContextMenuStrip();
        menu.Items.Add("Capture region",          null, (_, _) => StartCapture());
        menu.Items.Add("Capture window…",         null, (_, _) => StartWindowCapture());
        menu.Items.Add("Scrolling capture…",      null, (_, _) => StartScrollingCapture());
        menu.Items.Add("Instant full-screen save", null, (_, _) => InstantFullScreenSave());
        menu.Items.Add(new WF.ToolStripSeparator());
        menu.Items.Add("Color picker",            null, (_, _) => StartColorPicker());
        menu.Items.Add("OCR on selection",        null, (_, _) => StartOcr());
        menu.Items.Add("Pixel ruler",             null, (_, _) => ShowPixelRuler());
        menu.Items.Add(new WF.ToolStripSeparator());
        menu.Items.Add("Open Snapboard",          null, (_, _) => ShowMainWindow());
        menu.Items.Add("Settings…",               null, (_, _) => OpenSettings());
        menu.Items.Add("About Snapboard",         null, (_, _) => OpenAbout());
        menu.Items.Add(new WF.ToolStripSeparator());
        menu.Items.Add("Close tool overlays",     null, (_, _) => CloseToolOverlays());
        menu.Items.Add("Exit",                    null, (_, _) => ExitApp());
        _tray.ContextMenuStrip = menu;
    }

    /// <summary>
    /// Emergency recovery: force-closes every non-main window (capture overlays,
    /// OCR selection/result, color picker, pixel ruler, settings, etc.) so the
    /// desktop is always reachable even if a tool window ended up stuck.
    /// </summary>
    public void CloseToolOverlays()
    {
        var toClose = new List<Window>();
        foreach (Window w in Windows)
        {
            if (w is MainWindow) continue;
            toClose.Add(w);
        }
        foreach (var w in toClose)
        {
            try { w.Close(); }
            catch { /* best-effort */ }
        }
    }

    private void OnTrayMouseClick(object? sender, WF.MouseEventArgs e)
    {
        if (e.Button != WF.MouseButtons.Left) return;
        if (Settings.TrayClickCaptures) StartCapture();
        else ShowMainWindow();
    }

    // ---------------- Hotkeys ----------------

    public void ApplyHotkeys()
    {
        if (_hotkey == null) return;
        _hotkey.UnregisterAll();
        HotkeysSuspended = false;

        var failures = new List<HotkeyFailure>();

        CaptureHotkeyRegistered     = TryRegister("Capture",       Settings.CaptureHotkey,           () => Dispatcher.Invoke(StartCapture),          failures);
        FullScreenHotkeyRegistered  = TryRegister("Full-screen",   Settings.InstantFullScreenHotkey, () => Dispatcher.Invoke(InstantFullScreenSave), failures);
        ColorPickerHotkeyRegistered = TryRegister("Color picker",  Settings.ColorPickerHotkey,       () => Dispatcher.Invoke(StartColorPicker),      failures);
        OcrHotkeyRegistered         = TryRegister("OCR",           Settings.OcrHotkey,               () => Dispatcher.Invoke(StartOcr),              failures);
        PixelRulerHotkeyRegistered  = TryRegister("Pixel ruler",   Settings.PixelRulerHotkey,        () => Dispatcher.Invoke(ShowPixelRuler),        failures);

        HotkeyFailures = failures;

        if (MainWindow is MainWindow mw) mw.RefreshFromSettings();
    }

    private bool TryRegister(string name, string? hotkey, Action action, List<HotkeyFailure> failures)
    {
        if (string.IsNullOrWhiteSpace(hotkey) || _hotkey == null) return false;
        var spec = HotkeySpec.TryParse(hotkey);
        if (spec == null)
        {
            failures.Add(new HotkeyFailure(name, hotkey!));
            return false;
        }
        if (!_hotkey.TryRegister(spec.Key, action, spec.Modifiers))
        {
            failures.Add(new HotkeyFailure(name, hotkey!));
            return false;
        }
        return true;
    }

    /// <summary>True while the Settings window is open — our own hotkeys are
    /// unregistered during this time so <see cref="TestHotkey"/> can probe the
    /// OS without fighting our existing bindings.</summary>
    public bool HotkeysSuspended { get; private set; }

    /// <summary>Unregisters every app hotkey. Paired with
    /// <see cref="ResumeHotkeys"/> so the Settings dialog can probe hotkeys
    /// freely without our own bindings producing false "in use" results.</summary>
    public void SuspendHotkeys()
    {
        if (HotkeysSuspended || _hotkey == null) return;
        _hotkey.UnregisterAll();
        CaptureHotkeyRegistered = FullScreenHotkeyRegistered = ColorPickerHotkeyRegistered
            = OcrHotkeyRegistered = PixelRulerHotkeyRegistered = false;
        HotkeysSuspended = true;
    }

    /// <summary>Re-applies hotkeys from current settings after a matching
    /// <see cref="SuspendHotkeys"/> call. Safe to call even if we weren't
    /// actually suspended.</summary>
    public void ResumeHotkeys()
    {
        if (!HotkeysSuspended) { ApplyHotkeys(); return; }
        HotkeysSuspended = false;
        ApplyHotkeys();
    }

    /// <summary>
    /// Attempts a real Win32 <c>RegisterHotKey</c> probe for the given combo
    /// using a throwaway <see cref="HotkeyManager"/>. Returns true if the OS
    /// accepted it (combo is free), false otherwise (invalid syntax or
    /// already owned by another process).
    ///
    /// Call this while the app's own hotkeys are suspended (via
    /// <see cref="SuspendHotkeys"/>) so our existing bindings don't fight the
    /// probe — otherwise re-entering the same combo that we already own would
    /// report a false conflict.
    /// </summary>
    public bool TestHotkey(string? hotkey)
    {
        if (string.IsNullOrWhiteSpace(hotkey)) return true;
        var spec = HotkeySpec.TryParse(hotkey);
        if (spec == null) return false;

        using var tester = new HotkeyManager();
        if (!tester.TryRegister(spec.Key, () => { }, spec.Modifiers))
            return false;
        tester.UnregisterAll();
        return true;
    }

    /// <summary>
    /// Tries to apply and persist the given settings. If any hotkey fails to
    /// bind, this method rolls back to the previous settings + registrations
    /// and returns the failure list so Settings can show inline conflict errors.
    /// </summary>
    public IReadOnlyList<HotkeyFailure> SaveSettingsAndApply(AppSettings next)
    {
        // Keep a rollback snapshot so failed saves never leave the app with
        // partially bound hotkeys or conflict values persisted to disk.
        var previous = Settings.Clone();

        Settings = next;
        ApplyHotkeys();

        if (HotkeyFailures.Count > 0)
        {
            var failures = HotkeyFailures.ToArray();

            // Revert in-memory settings + registrations back to the last
            // known-good state when any hotkey could not be registered.
            Settings = previous;
            ApplyHotkeys();
            return failures;
        }

        SettingsService.Save(next);
        StartupRegistration.Apply(next.RunOnStartup);
        return HotkeyFailures;
    }

    private static string FormatFailures(IEnumerable<HotkeyFailure> failures)
        => string.Join(", ", failures.Select(f => $"{f.Name} ({f.Hotkey})"));

    // ---------------- Capture flows ----------------

    public void StartCapture()
    {
        foreach (Window w in Windows)
        {
            if (w is CaptureWindow) return;
        }
        var window = new CaptureWindow();
        window.Show();
        window.Activate();
    }

    /// <summary>
    /// PicPick-style window capture: shows a small dropdown listing every
    /// visible window. When the user picks one, Snapboard captures it via
    /// <c>PrintWindow(PW_RENDERFULLCONTENT)</c>, copies the bitmap to the
    /// clipboard, and shows a tray toast. No editor, no intermediate viewer.
    /// </summary>
    public void StartWindowCapture()
    {
        foreach (Window w in Windows)
        {
            if (w is WindowCaptureDialog existing)
            {
                existing.Activate();
                return;
            }
        }

        var dlg = new WindowCaptureDialog
        {
            Owner = MainWindow is MainWindow mw && mw.IsVisible ? mw : null,
        };

        bool? ok = dlg.ShowDialog();
        if (ok != true || dlg.PickedWindow is not { } info) return;

        // Do the capture on a background thread so the dialog closes instantly
        // and the UI stays responsive even when the target window is large.
        _ = Task.Run(() =>
        {
            System.Drawing.Bitmap? shot = null;
            string? error = null;
            try
            {
                shot = WindowEnumerator.CaptureWindow(info.Handle, bringToFront: true);
                if (shot == null)
                {
                    error = "Windows refused to render that window. Try bringing it to the front and retry.";
                }
            }
            catch (Exception ex)
            {
                error = ex.Message;
            }

            Dispatcher.Invoke(() =>
            {
                if (IsShuttingDown) { shot?.Dispose(); return; }

                if (error != null || shot == null)
                {
                    NotifyInfo("Window capture failed",
                        error ?? "Unknown error while capturing that window.",
                        WF.ToolTipIcon.Error);
                    shot?.Dispose();
                    return;
                }

                BitmapSource? bs = null;
                bool copied = false;
                try
                {
                    bs = ScreenCapture.ToBitmapSource(shot) as BitmapSource;
                    if (bs != null)
                    {
                        try { Clipboard.SetImage(bs); copied = true; }
                        catch { /* clipboard can be momentarily locked */ }
                    }
                }
                catch (Exception ex)
                {
                    NotifyInfo("Clipboard error",
                        "Captured the window but couldn't copy it: " + ex.Message,
                        WF.ToolTipIcon.Error);
                }

                string label = string.IsNullOrWhiteSpace(info.Title) ? "Window" : info.Title;
                if (label.Length > 60) label = label[..60].TrimEnd() + "…";

                string? savedPath = null;
                if (bs != null)
                {
                    try { savedPath = PromptSaveCapturedWindow(bs, label); }
                    catch (Exception ex)
                    {
                        NotifyInfo("Save failed",
                            "Could not write the window capture: " + ex.Message,
                            WF.ToolTipIcon.Error);
                    }
                }

                if (savedPath != null)
                {
                    NotifyInfo("Window captured",
                        $"{label} ({shot.Width}×{shot.Height}){(copied ? " copied to clipboard and" : "")} saved to {Path.GetFileName(savedPath)}.");
                }
                else if (copied)
                {
                    NotifyInfo("Window captured",
                        $"{label} copied to clipboard ({shot.Width}×{shot.Height}).");
                }

                shot.Dispose();
            });
        });
    }

    /// <summary>
    /// Prompts the user for a destination file and, if confirmed, writes the
    /// captured window bitmap to disk honouring the configured default format
    /// and JPEG quality. Returns the saved path, or <c>null</c> if the user
    /// cancelled the dialog.
    /// </summary>
    private string? PromptSaveCapturedWindow(BitmapSource src, string label)
    {
        bool isJpeg = Settings.DefaultFormat.Equals("jpg", StringComparison.OrdinalIgnoreCase)
                   || Settings.DefaultFormat.Equals("jpeg", StringComparison.OrdinalIgnoreCase);
        string ext = isJpeg ? ".jpg" : ".png";

        string safeLabel = SanitiseForFileName(label);
        string fileName = $"Snapboard-{(string.IsNullOrWhiteSpace(safeLabel) ? "Window" : safeLabel)}-{DateTime.Now:yyyyMMdd-HHmmss}{ext}";

        string defaultDir = string.IsNullOrWhiteSpace(Settings.SaveDirectory)
            ? Environment.GetFolderPath(Environment.SpecialFolder.MyPictures)
            : Settings.SaveDirectory;

        var dlg = new Microsoft.Win32.SaveFileDialog
        {
            Title = "Save window capture",
            Filter = isJpeg
                ? "JPEG image (*.jpg)|*.jpg|PNG image (*.png)|*.png"
                : "PNG image (*.png)|*.png|JPEG image (*.jpg)|*.jpg",
            FileName = fileName,
            InitialDirectory = defaultDir,
            AddExtension = true,
            OverwritePrompt = true,
        };

        Window? owner = MainWindow is MainWindow mw && mw.IsVisible ? mw : null;
        bool? ok = owner != null ? dlg.ShowDialog(owner) : dlg.ShowDialog();
        if (ok != true) return null;

        BitmapSaver.Save(src, dlg.FileName, Settings.JpegQuality);
        return dlg.FileName;
    }

    private static string SanitiseForFileName(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;
        var invalid = Path.GetInvalidFileNameChars();
        var sb = new System.Text.StringBuilder(value.Length);
        foreach (char c in value)
        {
            if (Array.IndexOf(invalid, c) >= 0 || c == '…') sb.Append('_');
            else sb.Append(c);
        }
        string cleaned = sb.ToString().Trim().TrimEnd('.', ' ');
        if (cleaned.Length > 40) cleaned = cleaned[..40].TrimEnd();
        return cleaned;
    }

    /// <summary>
    /// PicPick-style scrolling capture: the user clicks a scrollable window,
    /// Snapboard auto-scrolls it by synthesising wheel events, captures each
    /// frame, stitches the result, and saves the combined image — no manual
    /// scrolling required.
    /// </summary>
    public void StartScrollingCapture()
    {
        foreach (Window w in Windows)
        {
            if (w is ScrollingSelectorWindow || w is ScrollingSessionWindow)
            {
                w.Activate();
                return;
            }
        }

        var selector = new ScrollingSelectorWindow();
        selector.Closed += (_, _) =>
        {
            if (selector.PickedHwnd == IntPtr.Zero) return;

            // Let the selector's fade-out finish and the target window come
            // back to the foreground before we start firing wheel events.
            Dispatcher.BeginInvoke(new Action(() =>
            {
                var session = new ScrollingSessionWindow(
                    selector.PickedHwnd,
                    selector.ClickScreenPoint,
                    selector.PickedScreenRect);
                session.Show();
            }), System.Windows.Threading.DispatcherPriority.Background);
        };
        selector.Show();
        selector.Activate();
    }

    public void StartColorPicker()
    {
        foreach (Window w in Windows)
        {
            if (w is ColorPickerWindow) return;
        }
        var window = new ColorPickerWindow();
        window.Show();
        window.Activate();
    }

    public void StartOcr()
    {
        foreach (Window w in Windows)
        {
            if (w is OcrSelectionWindow) return;
        }
        var window = new OcrSelectionWindow();
        window.Show();
        window.Activate();
    }

    /// <summary>
    /// Runs OCR on the given bitmap entirely on a background thread.
    /// No window is created until text is ready, so a slow or stuck OCR
    /// engine can never freeze the UI. Status is surfaced via tray toasts.
    /// </summary>
    public void StartOcrFromBitmap(System.Drawing.Bitmap bmp)
    {
        NotifyInfo("OCR", "Reading text from your selection…");

        var cts = new System.Threading.CancellationTokenSource(TimeSpan.FromSeconds(45));

        _ = Task.Run(async () =>
        {
            bool hasOutcome = false;
            OcrService.OcrOutcome outcome = default;
            string? error = null;

            try
            {
                outcome = await OcrService.RecognizeAsync(bmp, cancellationToken: cts.Token).ConfigureAwait(false);
                hasOutcome = true;
            }
            catch (OperationCanceledException)
            {
                error = "OCR took longer than 45s and was cancelled.";
            }
            catch (Exception ex)
            {
                error = ex.Message;
            }
            finally
            {
                bmp.Dispose();
                cts.Dispose();
            }

            await Dispatcher.InvokeAsync(() =>
            {
                if (IsShuttingDown) return;

                if (error != null)
                {
                    NotifyInfo("OCR error", error, WF.ToolTipIcon.Error);
                    return;
                }

                if (!hasOutcome || !outcome.Success)
                {
                    NotifyInfo("OCR failed", outcome.Message ?? "Unknown error.", WF.ToolTipIcon.Error);
                    return;
                }

                if (string.IsNullOrWhiteSpace(outcome.Text))
                {
                    NotifyInfo("OCR", "No text was recognized in the selection.", WF.ToolTipIcon.Warning);
                    return;
                }

                var result = new OcrResultWindow(outcome.Text, outcome.LanguageTag)
                {
                    WindowStartupLocation = WindowStartupLocation.CenterScreen,
                };
                result.Show();
                result.Activate();
            });
        });
    }

    public void ShowPixelRuler()
    {
        foreach (Window w in Windows)
        {
            if (w is PixelRulerWindow existing)
            {
                existing.Activate();
                return;
            }
        }
        var ruler = new PixelRulerWindow();
        ruler.Show();
    }

    public void InstantFullScreenSave()
    {
        try
        {
            using var bmp = ScreenCapture.CapturePrimaryScreen(includeCursor: Settings.CaptureCursor);
            var dir = SettingsService.ResolveSaveDirectory(Settings);
            Directory.CreateDirectory(dir);
            var ext = Settings.DefaultFormat.Equals("jpg", StringComparison.OrdinalIgnoreCase) ? ".jpg" : ".png";
            var path = Path.Combine(dir, BitmapSaver.BuildDefaultFileName(ext));
            BitmapSaver.Save(bmp, path, Settings.JpegQuality);
            NotifyTray("Screenshot saved", path);
        }
        catch (Exception ex)
        {
            NotifyTray("Could not save screenshot", ex.Message, WF.ToolTipIcon.Error);
        }
    }

    public void ShowMainWindow()
    {
        if (MainWindow == null) return;
        if (!MainWindow.IsVisible) MainWindow.Show();
        if (MainWindow.WindowState == WindowState.Minimized) MainWindow.WindowState = WindowState.Normal;
        MainWindow.Activate();
        MainWindow.Topmost = true;
        MainWindow.Topmost = false;
    }

    public void OpenSettings()
    {
        var owner = MainWindow;
        if (owner != null && !owner.IsVisible) ShowMainWindow();

        // Suspend our own hotkey bindings for the lifetime of the dialog so
        // its live validation can probe the OS without our existing
        // registrations producing false "already in use" conflicts. Save
        // will re-apply regardless; if the user Cancels, the finally block
        // restores them from the unchanged settings.
        SuspendHotkeys();
        try
        {
            var dlg = new SettingsWindow(Settings) { Owner = owner };
            dlg.ShowDialog();
        }
        finally
        {
            ResumeHotkeys();
            if (MainWindow is MainWindow mw) mw.RefreshFromSettings();
        }
    }

    public void OpenAbout()
    {
        var owner = MainWindow;
        if (owner != null && !owner.IsVisible) ShowMainWindow();

        // Reuse an existing About dialog if one is already open instead of
        // stacking duplicates from repeated tray clicks.
        foreach (Window w in Windows)
        {
            if (w is AboutWindow existing)
            {
                existing.Activate();
                return;
            }
        }

        var dlg = new AboutWindow { Owner = owner };
        dlg.ShowDialog();
    }

    public void ShowTrayBalloonOnce(string title, string text)
    {
        if (_tray == null || _balloonShown) return;
        _balloonShown = true;
        NotifyTray(title, text);
    }

    /// <summary>Public balloon notification for tool windows.</summary>
    public void NotifyInfo(string title, string text, WF.ToolTipIcon icon = WF.ToolTipIcon.Info)
        => NotifyTray(title, text, icon);

    private void NotifyTray(string title, string text, WF.ToolTipIcon icon = WF.ToolTipIcon.Info)
    {
        if (_tray == null) return;
        _tray.BalloonTipTitle = title;
        _tray.BalloonTipText = text;
        _tray.BalloonTipIcon = icon;
        _tray.ShowBalloonTip(3000);
    }

    // ---------------- Lifecycle ----------------

    private void ExitApp()
    {
        IsShuttingDown = true;
        Shutdown();
    }

    private void Application_Exit(object sender, ExitEventArgs e)
    {
        _hotkey?.Dispose();
        if (_tray != null)
        {
            _tray.Visible = false;
            _tray.Dispose();
        }
    }

    private static Icon? LoadAppIcon()
    {
        try
        {
            var uri = new Uri("pack://application:,,,/Assets/snapboard.ico", UriKind.Absolute);
            StreamResourceInfo? info = GetResourceStream(uri);
            if (info?.Stream == null) return null;
            using var s = info.Stream;
            return new Icon(s);
        }
        catch
        {
            return null;
        }
    }
}
