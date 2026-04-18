using System.Drawing;
using System.IO;
using System.Windows;
using System.Windows.Media.Imaging;
using System.Windows.Resources;
using Snapboard.ColorPicker;
using Snapboard.Helpers;
using Snapboard.Ocr;
using Snapboard.Qr;
using Snapboard.Ruler;
using Snapboard.ScrollingCapture;
using Snapboard.Settings;
using Snapboard.Updates;
using System.Windows.Threading;
using WF = System.Windows.Forms;

namespace Snapboard;

public partial class App : Application
{
    private WF.NotifyIcon? _tray;
    private HotkeyManager? _hotkey;
    private bool _balloonShown;
    private bool _startupHotkeyAlertShown;
    private DispatcherTimer? _updateCheckTimer;
    private bool _updatePromptOpen;

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
    public bool QrHotkeyRegistered { get; private set; }
    public bool IsShuttingDown { get; private set; }

    /// <summary>A hotkey that couldn't be bound — usually because another app owns it.</summary>
    public record HotkeyFailure(string Name, string Hotkey);

    /// <summary>Populated by the most recent <see cref="ApplyHotkeys"/> call.</summary>
    public IReadOnlyList<HotkeyFailure> HotkeyFailures { get; private set; } = Array.Empty<HotkeyFailure>();

    private void Application_Startup(object sender, StartupEventArgs e)
    {
        LaunchedAsAutoStart = e.Args.Any(a =>
            string.Equals(a, StartupRegistration.AutoStartArg, StringComparison.OrdinalIgnoreCase));

        // NOTE: we intentionally do NOT apply WDA_EXCLUDEFROMCAPTURE to
        // every Snapboard window. Earlier versions of this code did, but on
        // Remote Desktop, some VM/virtual-display setups, and a few older
        // GPU drivers the flag makes the window render completely black or
        // invisible to the user (not just to capture APIs). The dashboard,
        // settings, and editor windows therefore stay unflagged — bleed
        // through is prevented by physically hiding them during a capture
        // via HideForCapture/RestoreAfterCapture. Only the tiny scroll-
        // capture overlay windows (session toolbar, target outline) use
        // WDA_EXCLUDEFROMCAPTURE, and each opts in explicitly.

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
            StartAutoUpdateChecks();
        }));
    }

    // ---------------- Auto-update ----------------

    /// <summary>
    /// Schedules background update checks. Runs one check a few seconds
    /// after startup (so the first-run UI isn't blocked), then every 24 h
    /// while the app is running. Respects <see cref="AppSettings.AutoCheckUpdates"/>
    /// and the user's "skip this version" preference.
    /// </summary>
    private void StartAutoUpdateChecks()
    {
        // Stagger the first check 5 s in so we don't race with tray
        // initialisation / hotkey registration on slow machines.
        Dispatcher.BeginInvoke(new Action(() =>
        {
            _ = RunUpdateCheckAsync(manual: false);
        }), DispatcherPriority.ApplicationIdle);

        _updateCheckTimer?.Stop();
        _updateCheckTimer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromHours(24),
        };
        _updateCheckTimer.Tick += (_, _) => _ = RunUpdateCheckAsync(manual: false);
        _updateCheckTimer.Start();
    }

    /// <summary>Tray menu "Check for updates…" entry point — forces a
    /// check even when the last automatic check was recent and ignores the
    /// "skipped version" preference so manual checks always prompt.</summary>
    public void CheckForUpdatesManually()
    {
        _ = RunUpdateCheckAsync(manual: true);
    }

    private async Task RunUpdateCheckAsync(bool manual)
    {
        if (IsShuttingDown) return;
        if (_updatePromptOpen) return;

        // Automatic checks are throttled to once per 24 h and gated on the
        // user preference. Manual checks always run.
        if (!manual)
        {
            if (!Settings.AutoCheckUpdates) return;
            if (Settings.LastUpdateCheckUtc is DateTime last &&
                DateTime.UtcNow - last < TimeSpan.FromHours(20))
            {
                return;
            }
        }

        Settings.LastUpdateCheckUtc = DateTime.UtcNow;
        SettingsService.Save(Settings);

        UpdateInfo? latest = null;
        try
        {
            latest = await UpdateService.GetLatestReleaseAsync().ConfigureAwait(true);
        }
        catch
        {
            // Network blips are not user-facing for automatic checks.
        }

        if (latest == null)
        {
            if (manual)
            {
                NotifyInfo("No updates available",
                    "Could not reach GitHub. Check your internet connection and try again.",
                    WF.ToolTipIcon.Info);
            }
            return;
        }

        var current = UpdateService.GetCurrentVersion();
        if (latest.Version <= current)
        {
            if (manual)
            {
                NotifyInfo("Snapboard is up to date",
                    $"You're running the latest version ({current}).");
            }
            return;
        }

        // Automatic checks honour the "skip this version" preference; the
        // manual menu entry deliberately ignores it so users can always
        // reopen the prompt.
        if (!manual &&
            !string.IsNullOrWhiteSpace(Settings.SkippedUpdateVersion) &&
            string.Equals(Settings.SkippedUpdateVersion, latest.Version.ToString(), StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        ShowUpdatePrompt(latest, current);
    }

    private void ShowUpdatePrompt(UpdateInfo info, Version current)
    {
        if (_updatePromptOpen) return;
        _updatePromptOpen = true;
        try
        {
            var dlg = new UpdatePromptWindow(info, current)
            {
                Owner = MainWindow is MainWindow mw && mw.IsVisible ? mw : null,
            };
            dlg.ShowDialog();

            switch (dlg.Choice)
            {
                case UpdatePromptWindow.UpdateChoice.Install:
                    // Installer already launched by the dialog; exit so it
                    // can replace our binaries without "file in use" errors.
                    ExitApp();
                    return;
                case UpdatePromptWindow.UpdateChoice.Skip:
                    Settings.SkippedUpdateVersion = info.Version.ToString();
                    SettingsService.Save(Settings);
                    break;
                case UpdatePromptWindow.UpdateChoice.Later:
                    // No persistent state — we'll re-prompt in ≤24 h.
                    break;
            }
        }
        finally
        {
            _updatePromptOpen = false;
        }
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

        // The WinForms ContextMenuStrip renderer reserves the right edge of
        // each row for "ShortcutKeyDisplayString", which is exactly what
        // we want for hotkey hints — it right-aligns the combo in a muted
        // colour without eating the item's text. We don't use the built-in
        // ShortcutKeys property because that would register the shortcut
        // on the menu (which only works when the menu owner has focus) —
        // our real hotkeys are process-wide via RegisterHotKey, and this
        // is purely a visual hint.
        AddItem(menu, "Capture region",            Settings.CaptureHotkey,           (_, _) => StartCapture());
        AddItem(menu, "Capture window…",           null,                             (_, _) => StartWindowCapture());
        AddItem(menu, "Scrolling capture…",        null,                             (_, _) => StartScrollingCapture());
        AddItem(menu, "Instant full-screen save",  Settings.InstantFullScreenHotkey, (_, _) => InstantFullScreenSave());
        menu.Items.Add(new WF.ToolStripSeparator());
        AddItem(menu, "Color picker",              Settings.ColorPickerHotkey,       (_, _) => StartColorPicker());
        AddItem(menu, "OCR on selection",          Settings.OcrHotkey,               (_, _) => StartOcr());
        AddItem(menu, "Scan QR code",              Settings.QrHotkey,                (_, _) => StartQrScan());
        AddItem(menu, "Pixel ruler",               Settings.PixelRulerHotkey,        (_, _) => ShowPixelRuler());
        menu.Items.Add(new WF.ToolStripSeparator());
        AddItem(menu, "Open Snapboard",            null, (_, _) => ShowMainWindow());
        AddItem(menu, "Settings…",                 null, (_, _) => OpenSettings());
        AddItem(menu, "Check for updates…",        null, (_, _) => CheckForUpdatesManually());
        AddItem(menu, "About Snapboard",           null, (_, _) => OpenAbout());
        menu.Items.Add(new WF.ToolStripSeparator());
        AddItem(menu, "Close tool overlays",       null, (_, _) => CloseToolOverlays());
        AddItem(menu, "Exit",                      null, (_, _) => ExitApp());

        _tray.ContextMenuStrip = menu;
    }

    /// <summary>Adds a tray-menu item and — if <paramref name="hotkey"/> is
    /// non-empty — shows the hotkey combo right-aligned next to the label
    /// via <c>ShortcutKeyDisplayString</c>. The click handler and the
    /// label text behave exactly the same whether or not a hotkey exists.</summary>
    private static void AddItem(WF.ContextMenuStrip menu, string text, string? hotkey, EventHandler onClick)
    {
        var item = new WF.ToolStripMenuItem(text);
        item.Click += onClick;
        if (!string.IsNullOrWhiteSpace(hotkey))
        {
            item.ShortcutKeyDisplayString = hotkey;
        }
        menu.Items.Add(item);
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
        QrHotkeyRegistered          = TryRegister("QR scan",       Settings.QrHotkey,                () => Dispatcher.Invoke(StartQrScan),           failures);

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
            = OcrHotkeyRegistered = PixelRulerHotkeyRegistered = QrHotkeyRegistered = false;
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

        // Rebuild the tray menu so its hotkey hints reflect the new combos.
        BuildTrayMenu();

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

        // Hide the dashboard (and any other Snapboard windows) during the
        // capture so they never end up in the screenshot. This matters for
        // the `CaptureScreenRegion` fallback path which literally grabs
        // pixels off the screen — without the hide, the dashboard's
        // close/fade animation can bleed into the capture. We remember
        // which windows we hid so we can restore them after the save flow.
        var hiddenWindows = HideForCapture();

        // Do the capture on a background thread so the dialog closes instantly
        // and the UI stays responsive even when the target window is large.
        _ = Task.Run(async () =>
        {
            // Give Windows a beat (1 s) to finish its DWM close/hide
            // animations on the capture dialog and the dashboard before we
            // start grabbing pixels — otherwise the fade-out of our own
            // windows can land in the screenshot.
            await Task.Delay(1000).ConfigureAwait(false);

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
                if (IsShuttingDown) { shot?.Dispose(); RestoreAfterCapture(hiddenWindows); return; }

                if (error != null || shot == null)
                {
                    NotifyInfo("Window capture failed",
                        error ?? "Unknown error while capturing that window.",
                        WF.ToolTipIcon.Error);
                    shot?.Dispose();
                    RestoreAfterCapture(hiddenWindows);
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
                RestoreAfterCapture(hiddenWindows);
            });
        });
    }

    /// <summary>
    /// Hides every visible Snapboard-owned window (dashboard, settings,
    /// about, etc.) before a screen-based capture. Returns the list of
    /// windows that were hidden so the caller can make them visible again
    /// once the capture is done. Windows of any type in <paramref name="except"/>
    /// stay visible — used by scrolling capture to keep its own session
    /// toolbar on screen while hiding everything else.
    /// </summary>
    public List<Window> HideForCapture(params Type[] except)
    {
        var hidden = new List<Window>();
        foreach (Window w in Windows)
        {
            if (w is WindowCaptureDialog) continue; // already closing
            if (!w.IsVisible) continue;
            if (except.Length > 0)
            {
                bool skip = false;
                foreach (var t in except)
                {
                    if (t.IsInstanceOfType(w)) { skip = true; break; }
                }
                if (skip) continue;
            }
            try
            {
                w.Hide();
                hidden.Add(w);
            }
            catch { /* best-effort */ }
        }
        return hidden;
    }

    /// <summary>Companion to <see cref="HideForCapture"/> — re-shows any
    /// windows we hid. Safe to call with an empty list.</summary>
    public static void RestoreAfterCapture(List<Window> hidden)
    {
        foreach (var w in hidden)
        {
            try
            {
                if (!w.IsVisible) w.Show();
            }
            catch { /* ignore */ }
        }
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
                    topHwnd:            selector.PickedHwnd,
                    captureHwnd:        selector.CaptureHwnd,
                    scrollAnchor:       selector.ClickScreenPoint,
                    initialCaptureRect: selector.PickedScreenRect);
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
    /// Opens the QR-code scan overlay. User drags a rectangle around a
    /// QR / barcode anywhere on screen; we crop that region and run
    /// decoding on a background thread, showing the decoded payload in a
    /// dark modal with Copy / Open-link actions.
    /// </summary>
    public void StartQrScan()
    {
        foreach (Window w in Windows)
        {
            if (w is QrSelectionWindow) return;
        }
        var window = new QrSelectionWindow();
        window.Show();
        window.Activate();
    }

    /// <summary>
    /// Runs QR decoding on the given bitmap entirely on a background
    /// thread. Mirrors <see cref="StartOcrFromBitmap"/>: no UI is created
    /// until decoding has an outcome, so a slow decode can never freeze
    /// the desktop. Errors are surfaced via tray toasts.
    /// </summary>
    public void StartQrFromBitmap(System.Drawing.Bitmap bmp)
    {
        _ = Task.Run(() =>
        {
            QrService.DecodeOutcome outcome;
            try
            {
                outcome = QrService.Decode(bmp);
            }
            finally
            {
                bmp.Dispose();
            }

            Dispatcher.Invoke(() =>
            {
                if (IsShuttingDown) return;

                if (!outcome.Success || outcome.Codes.Count == 0)
                {
                    NotifyInfo("No QR code found",
                        outcome.Message ?? "Nothing was detected in the selection.",
                        WF.ToolTipIcon.Warning);
                    return;
                }

                var result = new QrResultWindow(outcome.Codes)
                {
                    WindowStartupLocation = WindowStartupLocation.CenterScreen,
                };
                result.Show();
                result.Activate();
            });
        });
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
