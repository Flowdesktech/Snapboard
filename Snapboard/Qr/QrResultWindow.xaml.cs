using System.Diagnostics;
using System.Text;
using System.Windows;
using System.Windows.Input;
using Snapboard.Helpers;

namespace Snapboard.Qr;

/// <summary>
/// Dumb display-only modal for already-decoded QR payloads. Never touches
/// the decoder, never owns a bitmap, never awaits anything — mirrors the
/// design of <see cref="Ocr.OcrResultWindow"/>.
///
/// When the detected payload looks like an HTTP/HTTPS URL we surface an
/// extra "Open link" button that launches the default browser. For other
/// payloads (plain text, Wi-Fi configs, vCards, etc.) we just display the
/// raw decoded string.
/// </summary>
public partial class QrResultWindow : Window
{
    private readonly string _primaryText;
    private readonly bool _primaryIsUrl;

    public QrResultWindow(IReadOnlyList<QrService.DecodedCode> codes)
    {
        InitializeComponent();
        DarkTitleBar.Apply(this);

        if (codes == null || codes.Count == 0)
        {
            _primaryText = string.Empty;
            _primaryIsUrl = false;
            ResultBox.Text = string.Empty;
            MetaText.Text = "No QR codes were decoded.";
            CopyButton.IsEnabled = false;
            return;
        }

        // Concatenate multiple detected codes with blank lines between them,
        // but treat the FIRST code as "primary" for the Open-link shortcut
        // and clipboard default. This covers the overwhelmingly common case
        // of a single QR per selection without losing the rare multi-QR one.
        _primaryText = codes[0].Text ?? string.Empty;
        _primaryIsUrl = LooksLikeUrl(_primaryText);

        var sb = new StringBuilder();
        for (int i = 0; i < codes.Count; i++)
        {
            if (i > 0) sb.Append("\n\n");
            if (codes.Count > 1)
            {
                sb.Append("[").Append(i + 1).Append("] ").Append(codes[i].Format).Append(":\n");
            }
            sb.Append(codes[i].Text ?? string.Empty);
        }
        ResultBox.Text = sb.ToString();

        var formats = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var c in codes) formats.Add(c.Format);
        string formatLabel = string.Join(", ", formats);
        int chars = _primaryText.Length;
        MetaText.Text = codes.Count == 1
            ? $"{formatLabel}  ·  {chars} characters"
            : $"{codes.Count} codes detected  ·  {formatLabel}";

        CopyButton.IsEnabled = !string.IsNullOrEmpty(ResultBox.Text);
        OpenButton.Visibility = _primaryIsUrl ? Visibility.Visible : Visibility.Collapsed;

        Loaded += (_, _) =>
        {
            ResultBox.Focus();
            ResultBox.SelectAll();
        };
    }

    // ---------- Actions ----------

    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            e.Handled = true;
            Close();
        }
        else if (e.Key == Key.Enter && _primaryIsUrl)
        {
            e.Handled = true;
            TryOpenPrimary();
        }
    }

    private void OnCopyClick(object sender, RoutedEventArgs e)
    {
        try
        {
            // When there's just a single code we copy JUST the payload —
            // what the user would have scanned with their phone. For
            // multiple codes we copy the whole labelled text for context.
            string payload = string.IsNullOrEmpty(ResultBox.SelectedText)
                ? ResultBox.Text
                : ResultBox.SelectedText;
            Clipboard.SetText(payload);
            StatusText.Text = "Copied to clipboard.";
        }
        catch (Exception ex)
        {
            StatusText.Text = $"Clipboard error: {ex.Message}";
        }
    }

    private void OnOpenClick(object sender, RoutedEventArgs e) => TryOpenPrimary();

    private void OnCloseClick(object sender, RoutedEventArgs e) => Close();

    // ---------- Helpers ----------

    /// <summary>
    /// Opens <see cref="_primaryText"/> in the user's default browser after
    /// a final sanity check — we never launch anything unless the payload
    /// is explicitly <c>http(s)://…</c>. Custom schemes (<c>file:</c>,
    /// <c>javascript:</c>, app handlers, etc.) require the user to copy +
    /// paste manually, which is intentional for safety.
    /// </summary>
    private void TryOpenPrimary()
    {
        if (!_primaryIsUrl || string.IsNullOrWhiteSpace(_primaryText)) return;
        try
        {
            Process.Start(new ProcessStartInfo(_primaryText) { UseShellExecute = true });
            StatusText.Text = "Opened in your browser.";
        }
        catch (Exception ex)
        {
            StatusText.Text = $"Couldn't open link: {ex.Message}";
        }
    }

    private static bool LooksLikeUrl(string s)
    {
        if (string.IsNullOrWhiteSpace(s)) return false;
        if (s.Length > 4096) return false; // absurd URL → probably data, not a link
        return Uri.TryCreate(s, UriKind.Absolute, out var uri)
            && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
    }
}
