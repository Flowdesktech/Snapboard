using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using Snapboard.Controls;
using Snapboard.Helpers;
using Snapboard.Settings;
using Snapboard.Updates;
using WF = System.Windows.Forms;

namespace Snapboard;

public partial class SettingsWindow : Window
{
    private readonly AppSettings _working;

    /// <summary>Each hotkey field + its label + its inline error TextBlock,
    /// used by Save and by the live validator to find the right UI element
    /// to flag when a conflict is detected.</summary>
    private (string Label, HotkeyBox Box, TextBlock Error)[] _hotkeyFields = Array.Empty<(string, HotkeyBox, TextBlock)>();

    /// <summary>True once the window is fully loaded so the live validator
    /// doesn't run against half-initialised fields during ctor-time binding
    /// flushes.</summary>
    private bool _liveValidationReady;

    public bool SavedChanges { get; private set; }
    public AppSettings Result => _working;

    public SettingsWindow(AppSettings current)
    {
        InitializeComponent();
        DarkTitleBar.Apply(this);
        _working = current.Clone();
        DataContext = _working;

        FormatPng.IsChecked  = _working.DefaultFormat.Equals("png", StringComparison.OrdinalIgnoreCase);
        FormatJpeg.IsChecked = !FormatPng.IsChecked;

        var settingsPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Snapboard", "settings.json");
        SettingsPathText.Text = settingsPath;

        // Lazy-initialise the field lookup once all the named elements exist,
        // then wire every box up to the live validator so conflicts surface
        // the instant the user commits a new combination.
        Loaded += (_, _) =>
        {
            _hotkeyFields = new (string, HotkeyBox, TextBlock)[]
            {
                ("Capture",      CaptureHotkeyBox,     CaptureHotkeyError),
                ("Full-screen",  FullScreenHotkeyBox,  FullScreenHotkeyError),
                ("Color picker", ColorPickerHotkeyBox, ColorPickerHotkeyError),
                ("OCR",          OcrHotkeyBox,         OcrHotkeyError),
                ("QR scan",      QrHotkeyBox,          QrHotkeyError),
                ("Pixel ruler",  PixelRulerHotkeyBox,  PixelRulerHotkeyError),
            };

            foreach (var (_, box, _) in _hotkeyFields)
            {
                // React to any commit into the Hotkey DP — covers typing a new
                // combo, clearing with Delete, and two-way binding updates.
                var descriptor = System.ComponentModel.DependencyPropertyDescriptor
                    .FromProperty(HotkeyBox.HotkeyProperty, typeof(HotkeyBox));
                descriptor?.AddValueChanged(box, (_, _) => ValidateAllHotkeys());
            }

            _liveValidationReady = true;
            ValidateAllHotkeys();
        };
    }

    private void OnBrowseFolderClick(object sender, RoutedEventArgs e)
    {
        using var dlg = new WF.FolderBrowserDialog
        {
            Description = "Pick a folder for instant-saves and auto-saves",
            UseDescriptionForTitle = true,
            SelectedPath = string.IsNullOrWhiteSpace(_working.SaveDirectory)
                ? SettingsService.ResolveSaveDirectory(_working)
                : _working.SaveDirectory,
        };
        if (dlg.ShowDialog() == WF.DialogResult.OK)
        {
            _working.SaveDirectory = dlg.SelectedPath;
            // DataContext is the _working instance; re-assign for the TextBox to refresh.
            DataContext = null;
            DataContext = _working;
        }
    }

    /// <summary>
    /// "Check for updates now" button in the UPDATES card. Performs the
    /// check inline so the user gets immediate feedback (success, up-to-
    /// date, or failure) without dismissing the Settings window. Found
    /// updates are delegated to <see cref="App.CheckForUpdatesManually"/>
    /// which owns the prompt dialog.
    /// </summary>
    private async void OnCheckUpdatesClick(object sender, RoutedEventArgs e)
    {
        CheckUpdatesButton.IsEnabled = false;
        UpdateStatusText.Text = "Checking…";

        UpdateInfo? latest = null;
        try
        {
            latest = await UpdateService.GetLatestReleaseAsync();
        }
        catch { /* surfaced as null below */ }

        CheckUpdatesButton.IsEnabled = true;

        if (latest == null)
        {
            UpdateStatusText.Text = "Couldn't reach GitHub. Try again later.";
            return;
        }

        var current = UpdateService.GetCurrentVersion();
        if (latest.Version <= current)
        {
            UpdateStatusText.Text = $"You're on the latest version ({current}).";
            return;
        }

        UpdateStatusText.Text = $"Update available: {latest.Version}";
        if (Application.Current is App app)
        {
            // Close Settings first so the update prompt owns the foreground.
            Close();
            app.CheckForUpdatesManually();
        }
    }

    private void OnOpenSettingsFolderClick(object sender, RoutedEventArgs e)
    {
        var dir = Path.GetDirectoryName(SettingsPathText.Text);
        if (string.IsNullOrEmpty(dir)) return;
        Directory.CreateDirectory(dir);
        try { Process.Start(new ProcessStartInfo("explorer.exe", dir) { UseShellExecute = true }); }
        catch { /* ignore */ }
    }

    private void OnCancelClick(object sender, RoutedEventArgs e)
    {
        SavedChanges = false;
        Close();
    }

    private void OnSaveClick(object sender, RoutedEventArgs e)
    {
        _working.DefaultFormat = FormatJpeg.IsChecked == true ? "jpg" : "png";
        _working.JpegQuality   = (int)JpegQualitySlider.Value;

        // Re-run the same validation the live checker uses — if anything is
        // still red, refuse to save so the settings file never ends up with
        // a hotkey combo that's guaranteed not to bind.
        if (!ValidateAllHotkeys())
        {
            ShowBanner(HotkeyErrorBannerText.Text is { Length: > 0 } existing
                ? existing
                : "One or more hotkeys are invalid or already in use. Fix the highlighted field(s) and try again.");
            return;
        }

        // Everything checks out locally — persist and let App re-register
        // hotkeys. App also re-runs its own probe; any failure there gets
        // surfaced the same way as the live validation.
        var app = (App)Application.Current;
        var failures = app.SaveSettingsAndApply(_working);

        if (failures.Count > 0)
        {
            var failedByLabel = failures
                .GroupBy(f => f.Name, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

            foreach (var (label, _, error) in _hotkeyFields)
            {
                if (failedByLabel.TryGetValue(label, out var failure))
                {
                    ShowFieldError(error,
                        $"'{failure.Hotkey}' is already registered by another app. Pick a different combination.");
                }
            }

            var list = string.Join(", ", failures.Select(f => $"{f.Name} ({f.Hotkey})"));
            ShowBanner(
                $"These hotkeys are already in use by another app: {list}. " +
                "Pick a different combination for each highlighted field.");

            // Explicit alert so the registration failure is immediately obvious
            // even if the user misses the inline errors/banner.
            MessageBox.Show(
                this,
                "Hotkey registration failed for:\n\n" +
                $"{list}\n\n" +
                "Those shortcuts are already used by another app. " +
                "Please choose different combinations.",
                "Snapboard — hotkey registration failed",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);

            // Stay open so the user can adjust the offending field(s).
            return;
        }

        SavedChanges = true;
        Close();
    }

    /// <summary>
    /// Inspects every hotkey field against three things, in priority order:
    ///  1. Syntactic validity (<see cref="HotkeySpec.TryParse"/>).
    ///  2. Duplicates within the Settings dialog itself — two Snapboard
    ///     features can't share the same combo.
    ///  3. Real OS availability via <see cref="App.TestHotkey"/>, which does
    ///     a live <c>RegisterHotKey</c> probe while the app's own bindings
    ///     are suspended (see <see cref="App.OpenSettings"/>).
    /// Populates inline errors + the top banner for any problems and returns
    /// true only when every field is clean.
    /// </summary>
    private bool ValidateAllHotkeys()
    {
        if (!_liveValidationReady) return true;

        ClearHotkeyErrorUi();

        // Pull the LATEST value directly off each HotkeyBox so we're always
        // validating what the user sees, not a stale binding snapshot.
        var snapshot = _hotkeyFields
            .Select(f => (f.Label, Value: f.Box.Hotkey, f.Error))
            .ToArray();

        bool ok = true;

        // 1. Syntactic validity.
        foreach (var (label, value, error) in snapshot)
        {
            if (!string.IsNullOrWhiteSpace(value) && HotkeySpec.TryParse(value) == null)
            {
                ShowFieldError(error, $"'{value}' isn't a valid hotkey combination.");
                EnsureBanner($"{label} hotkey is invalid — pick a different combination.");
                ok = false;
            }
        }

        // 2. Dialog-internal duplicates.
        var seen = new Dictionary<string, (string Label, TextBlock Error)>(StringComparer.OrdinalIgnoreCase);
        foreach (var (label, value, error) in snapshot)
        {
            if (string.IsNullOrWhiteSpace(value)) continue;
            var spec = HotkeySpec.TryParse(value);
            if (spec == null) continue;
            var key = $"{spec.Modifiers}:{(int)spec.Key}";
            if (seen.TryGetValue(key, out var existing))
            {
                ShowFieldError(error,
                    $"Also assigned to \u201C{existing.Label}\u201D. Pick a different combination.");
                ShowFieldError(existing.Error,
                    $"Also assigned to \u201C{label}\u201D. Pick a different combination.");
                EnsureBanner(
                    $"{label} and {existing.Label} share the same shortcut ({spec.Display}).");
                ok = false;
                continue;
            }
            seen[key] = (label, error);
        }

        // 3. Real OS availability — only probe combos that passed the earlier
        // checks so we don't double-report the same field.
        var app = (App)Application.Current;
        foreach (var (label, value, error) in snapshot)
        {
            if (string.IsNullOrWhiteSpace(value)) continue;
            if (error.Visibility == Visibility.Visible) continue;

            if (!app.TestHotkey(value))
            {
                ShowFieldError(error,
                    $"'{value}' is already registered by another app. Pick a different combination.");
                EnsureBanner(
                    "One or more hotkeys are already in use by another app. " +
                    "Pick a different combination for each highlighted field.");
                ok = false;
            }
        }

        return ok;
    }

    private void ClearHotkeyErrorUi()
    {
        HotkeyErrorBanner.Visibility = Visibility.Collapsed;
        HotkeyErrorBannerText.Text = string.Empty;
        StatusText.Text = string.Empty;

        foreach (var (_, _, error) in _hotkeyFields)
        {
            error.Text = string.Empty;
            error.Visibility = Visibility.Collapsed;
        }
    }

    /// <summary>Sets the banner to <paramref name="message"/> only if it isn't
    /// already visible — lets the first detected issue win and keeps the
    /// banner from flickering between messages during a multi-field probe.</summary>
    private void EnsureBanner(string message)
    {
        if (HotkeyErrorBanner.Visibility == Visibility.Visible) return;
        ShowBanner(message);
    }

    private void ShowFieldError(TextBlock field, string message)
    {
        field.Text = message;
        field.Visibility = Visibility.Visible;
    }

    private void ShowBanner(string message)
    {
        HotkeyErrorBannerText.Text = message;
        HotkeyErrorBanner.Visibility = Visibility.Visible;

        // Keep StatusText in sync so the bottom strip still reflects save state,
        // but the banner is now the primary surface for the error.
        StatusText.Text = message;
        StatusText.Foreground = System.Windows.Media.Brushes.IndianRed;
    }
}
