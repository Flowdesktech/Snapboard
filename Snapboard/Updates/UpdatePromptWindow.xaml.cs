using System.Windows;
using Snapboard.Helpers;

namespace Snapboard.Updates;

/// <summary>
/// Dark-themed update prompt. Shows the new version, release notes, and
/// three choices: install now (downloads the installer and launches it),
/// remind me later (no persistent state), or skip this version
/// (remembers <c>SkippedUpdateVersion</c> in settings so we don't nag).
/// </summary>
public partial class UpdatePromptWindow : Window
{
    public enum UpdateChoice { None, Install, Later, Skip }

    private readonly UpdateInfo _info;
    private readonly Version _currentVersion;
    private CancellationTokenSource? _downloadCts;

    public UpdateChoice Choice { get; private set; } = UpdateChoice.None;

    public UpdatePromptWindow(UpdateInfo info, Version currentVersion)
    {
        _info = info;
        _currentVersion = currentVersion;
        InitializeComponent();
        Loaded += OnLoaded;
        Closing += OnClosing;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        DarkTitleBar.Apply(this);
        SubtitleText.Text =
            $"Snapboard {_info.Version} is ready to install. You're currently running {_currentVersion}.";

        // GitHub release bodies are Markdown. We don't render MD formally;
        // we just show the raw text (stripped of the "## Downloads" table
        // and similar boilerplate) so users can see what's new.
        ReleaseNotesText.Text = SummariseNotes(_info.ReleaseNotes);
    }

    private void OnClosing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        // If a download is in flight when the dialog is force-closed (X),
        // cancel it cleanly.
        try { _downloadCts?.Cancel(); } catch { }
    }

    // ---------------- Button handlers ----------------

    private void OnSkipClick(object sender, RoutedEventArgs e)
    {
        Choice = UpdateChoice.Skip;
        DialogResult = true;
        Close();
    }

    private void OnLaterClick(object sender, RoutedEventArgs e)
    {
        Choice = UpdateChoice.Later;
        DialogResult = true;
        Close();
    }

    private async void OnInstallClick(object sender, RoutedEventArgs e)
    {
        // Enter the "downloading" state: lock the dialog to prevent a double
        // click and surface the progress bar. If the download fails we
        // re-enable the buttons so the user can retry or dismiss.
        InstallButton.IsEnabled = false;
        LaterButton.IsEnabled = false;
        SkipButton.IsEnabled = false;
        ProgressArea.Visibility = Visibility.Visible;
        ProgressLabel.Text = "Downloading…";
        DownloadProgress.Value = 0;

        _downloadCts = new CancellationTokenSource();
        var progress = new Progress<double>(v =>
        {
            DownloadProgress.Value = v;
            int pct = (int)Math.Round(v * 100);
            ProgressLabel.Text = $"Downloading… {pct}%";
        });

        string? installerPath = null;
        try
        {
            installerPath = await UpdateService.DownloadInstallerAsync(_info, progress, _downloadCts.Token);
        }
        catch (OperationCanceledException)
        {
            // User closed the dialog; treat as a "later".
            return;
        }
        catch
        {
            // Swallow — handled below via null check.
        }

        if (installerPath == null)
        {
            ProgressLabel.Text = "Download failed. Check your internet connection and try again.";
            DownloadProgress.Value = 0;
            InstallButton.IsEnabled = true;
            LaterButton.IsEnabled = true;
            SkipButton.IsEnabled = true;
            return;
        }

        ProgressLabel.Text = "Launching installer…";
        DownloadProgress.Value = 1;

        bool launched = UpdateService.LaunchInstaller(installerPath);
        if (!launched)
        {
            ProgressLabel.Text = "Could not launch the installer. Try downloading manually from GitHub.";
            InstallButton.IsEnabled = true;
            LaterButton.IsEnabled = true;
            SkipButton.IsEnabled = true;
            return;
        }

        Choice = UpdateChoice.Install;
        DialogResult = true;
        Close();
    }

    // ---------------- Notes helper ----------------

    /// <summary>
    /// GitHub release bodies contain Markdown. We can't render it here, so
    /// we trim the obvious download/file tables and return the plain text
    /// up to ~1500 characters — just enough to surface the highlights.
    /// </summary>
    private static string SummariseNotes(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return "No release notes provided.";

        var lines = raw.Replace("\r\n", "\n").Split('\n');
        var kept = new List<string>(lines.Length);
        bool skipping = false;

        foreach (var line in lines)
        {
            var l = line.TrimEnd();
            if (l.StartsWith("## Downloads", StringComparison.OrdinalIgnoreCase) ||
                l.StartsWith("### Downloads", StringComparison.OrdinalIgnoreCase))
            {
                skipping = true;
                continue;
            }
            if (skipping)
            {
                // Exit the skip block when we hit the next heading.
                if (l.StartsWith("## ") || l.StartsWith("### "))
                {
                    skipping = false;
                }
                else
                {
                    continue;
                }
            }

            // Drop markdown emphasis markers so plain text looks clean.
            var cleaned = l
                .Replace("`", string.Empty)
                .Replace("**", string.Empty)
                .Replace("*", "•");
            kept.Add(cleaned);
        }

        var text = string.Join("\n", kept).Trim();
        if (text.Length > 1500) text = text[..1500].TrimEnd() + "\n\n…";
        return text.Length == 0 ? "No release notes provided." : text;
    }
}
