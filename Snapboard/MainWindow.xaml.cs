using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Navigation;
using Snapboard.Helpers;

namespace Snapboard;

public partial class MainWindow : Window
{
    // Warning-red foreground for hotkey chips whose registration failed.
    // Declared once so every chip shares the same brush instance.
    private static readonly SolidColorBrush ConflictBrush =
        (SolidColorBrush)new BrushConverter().ConvertFrom("#FFE0556B")!;

    public MainWindow()
    {
        InitializeComponent();
        DarkTitleBar.Apply(this);
        FooterVersionRun.Text = VersionInfo.DisplayWithV;
        ConflictBrush.Freeze();
        Loaded += (_, _) => RefreshFromSettings();
    }

    /// <summary>
    /// Re-reads settings from App and updates every hotkey chip / status label.
    /// Called after the settings window saves changes or hotkeys are re-applied.
    /// </summary>
    public void RefreshFromSettings()
    {
        var app = (App)Application.Current;

        // Render each chip from the actual registration result instead of only
        // relying on HotkeyFailures labels. This guarantees we never display a
        // hotkey value in the dashboard unless it is truly active right now.
        UpdateHotkeyChip(HeroHotkeyText,          app.Settings.CaptureHotkey,           app.CaptureHotkeyRegistered);
        UpdateHotkeyChip(ShortcutCaptureText,     app.Settings.CaptureHotkey,           app.CaptureHotkeyRegistered);
        UpdateHotkeyChip(ShortcutFullScreenText,  app.Settings.InstantFullScreenHotkey, app.FullScreenHotkeyRegistered);
        UpdateHotkeyChip(ShortcutColorPickerText, app.Settings.ColorPickerHotkey,       app.ColorPickerHotkeyRegistered);
        UpdateHotkeyChip(ShortcutOcrText,         app.Settings.OcrHotkey,               app.OcrHotkeyRegistered);
        UpdateHotkeyChip(ShortcutQrText,          app.Settings.QrHotkey,                app.QrHotkeyRegistered);
        UpdateHotkeyChip(ShortcutRulerText,       app.Settings.PixelRulerHotkey,        app.PixelRulerHotkeyRegistered);

        if (app.HotkeyFailures.Count > 0)
        {
            HotkeyStatusText.Text =
                $"{app.HotkeyFailures.Count} hotkey(s) already in use — open Settings to change them";
            HotkeyStatusText.Foreground = ConflictBrush;
        }
        else if (app.CaptureHotkeyRegistered)
        {
            HotkeyStatusText.Text = $"{app.Settings.CaptureHotkey} hotkey registered";
            HotkeyStatusText.Foreground = (Brush)FindResource("Muted");
        }
        else
        {
            HotkeyStatusText.Text = "Capture hotkey not set — use the Capture button or tray";
            HotkeyStatusText.Foreground = (Brush)FindResource("Muted");
        }
    }

    /// <summary>
    /// Renders a single Kbd chip's state. When a hotkey either isn't
    /// configured or couldn't be bound, we show a "—" placeholder (never
    /// the raw value) so the dashboard doesn't lie about what hotkeys are
    /// actually live. Failed hotkeys get a red tint + tooltip pointing
    /// the user at the Settings dialog.
    /// </summary>
    private void UpdateHotkeyChip(TextBlock chip, string? hotkey, bool registered)
    {
        bool empty = string.IsNullOrWhiteSpace(hotkey);

        if (empty || !registered)
        {
            chip.Text = "—";

            if (!empty)
            {
                chip.Foreground = ConflictBrush;
                chip.ToolTip =
                    $"'{hotkey}' is not active because registration failed. Open Settings to pick another combination.";
            }
            else
            {
                chip.Foreground = (Brush)FindResource("Muted");
                chip.ToolTip = "Not set — configure this hotkey in Settings.";
            }
        }
        else
        {
            chip.Text = hotkey!;
            chip.ClearValue(TextBlock.ForegroundProperty);
            chip.ToolTip = null;
        }
    }

    public void SetHotkeyStatus(bool registered) => RefreshFromSettings();

    private void OnCaptureClick(object sender, RoutedEventArgs e)          => ((App)Application.Current).StartCapture();
    private void OnCaptureWindowClick(object sender, RoutedEventArgs e)    => ((App)Application.Current).StartWindowCapture();
    private void OnScrollingCaptureClick(object sender, RoutedEventArgs e) => ((App)Application.Current).StartScrollingCapture();
    private void OnSettingsClick(object sender, RoutedEventArgs e)         => ((App)Application.Current).OpenSettings();
    private void OnAboutClick(object sender, RoutedEventArgs e)            => ((App)Application.Current).OpenAbout();
    private void OnColorPickerClick(object sender, RoutedEventArgs e)      => ((App)Application.Current).StartColorPicker();
    private void OnOcrClick(object sender, RoutedEventArgs e)              => ((App)Application.Current).StartOcr();
    private void OnQrClick(object sender, RoutedEventArgs e)               => ((App)Application.Current).StartQrScan();
    private void OnRulerClick(object sender, RoutedEventArgs e)            => ((App)Application.Current).ShowPixelRuler();
    private void OnHideToTrayClick(object sender, RoutedEventArgs e)  => HideToTray();
    private void OnExitClick(object sender, RoutedEventArgs e)        => Application.Current.Shutdown();

    /// <summary>
    /// Enforces "radio-group" behavior across the TOOLS chip panel and swaps
    /// the description text to describe whatever tool was clicked. The capture
    /// window picks its own tool when the user starts a capture — this panel
    /// is just a preview / discovery affordance on the dashboard.
    /// </summary>
    private void OnToolChipClick(object sender, RoutedEventArgs e)
    {
        if (sender is not ToggleButton clicked) return;

        // Prevent unchecking via a second click — one tool must always be shown.
        if (clicked.IsChecked != true)
        {
            clicked.IsChecked = true;
            return;
        }

        // Uncheck the other chips so we behave like a radio group.
        foreach (var child in ToolChipPanel.Children)
        {
            if (child is ToggleButton tb && !ReferenceEquals(tb, clicked))
                tb.IsChecked = false;
        }

        ToolDescriptionText.Text = (clicked.Tag as string) switch
        {
            "Pen"   => "The Pen tool draws freehand strokes — great for circling, underlining, or scribbling notes on top of a screenshot. Use the color and thickness controls on the capture toolbar to style it.",
            "Rect"  => "The Rectangle tool outlines a clean rectangular area — ideal for framing buttons, UI regions, or error messages you want to call out in a screenshot.",
            "Arrow" => "The Arrow tool points at something. Click and drag from the anchor to the target; the arrowhead scales with the current thickness setting.",
            "Text"  => "The Text tool drops an editable label on top of the screenshot. Use it for quick captions, annotations, or reviewer comments before copying or saving.",
            "Blur"  => "The Blur tool masks any rectangle you drag over it — useful for passwords, emails, tokens and other sensitive content before sharing a screenshot.",
            _       => ToolDescriptionText.Text,
        };
    }

    private void OnFooterLinkClick(object sender, RequestNavigateEventArgs e)
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = e.Uri.AbsoluteUri,
                UseShellExecute = true,
            });
        }
        catch { /* ignore — worst case the link just doesn't open */ }
        e.Handled = true;
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        // Clicking the window's [X] minimizes to the tray instead of exiting,
        // so the global hotkey keeps working. Exit via the tray menu
        // or the Exit button in the header.
        if (!((App)Application.Current).IsShuttingDown)
        {
            e.Cancel = true;
            HideToTray();
        }
        base.OnClosing(e);
    }

    private void HideToTray()
    {
        Hide();
        ((App)Application.Current).ShowTrayBalloonOnce(
            "Snapboard is still running",
            "Click the tray icon to capture, or right-click for more options.");
    }
}
