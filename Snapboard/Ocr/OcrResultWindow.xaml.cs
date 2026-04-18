using System.Windows;
using System.Windows.Input;
using Snapboard.Helpers;

namespace Snapboard.Ocr;

/// <summary>
/// Dumb display-only window for already-computed OCR text.
/// It never runs OCR, never owns a bitmap, never awaits anything.
/// </summary>
public partial class OcrResultWindow : Window
{
    public OcrResultWindow(string text, string? languageTag)
    {
        InitializeComponent();
        DarkTitleBar.Apply(this);

        ResultBox.Text = text ?? string.Empty;

        int charCount = ResultBox.Text.Length;
        int lineCount = charCount == 0 ? 0 : ResultBox.Text.Split('\n').Length;
        string meta = $"{charCount} characters  ·  {lineCount} lines";
        if (!string.IsNullOrWhiteSpace(languageTag)) meta += $"  ·  language: {languageTag}";
        if (charCount == 0) meta = "No text was recognized in the selection.";
        MetaText.Text = meta;

        CopyButton.IsEnabled = charCount > 0;

        Loaded += (_, _) =>
        {
            if (charCount > 0)
            {
                ResultBox.Focus();
                ResultBox.SelectAll();
            }
            else
            {
                Focus();
                Keyboard.Focus(this);
            }
        };
    }

    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            e.Handled = true;
            Close();
        }
    }

    private void OnCopyAllClick(object sender, RoutedEventArgs e)
    {
        try
        {
            Clipboard.SetText(ResultBox.Text);
            StatusText.Text = "Copied to clipboard.";
        }
        catch (Exception ex)
        {
            StatusText.Text = $"Clipboard error: {ex.Message}";
        }
    }

    private void OnCloseClick(object sender, RoutedEventArgs e) => Close();
}
