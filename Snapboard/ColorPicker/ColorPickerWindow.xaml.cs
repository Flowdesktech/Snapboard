using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Snapboard.Helpers;
using SD = System.Drawing;

namespace Snapboard.ColorPicker;

public partial class ColorPickerWindow : Window
{
    private enum Format { Hex, Rgb, Hsl }

    private SD.Bitmap? _screenBitmap;
    private BitmapSource? _screenSource;
    private Format _format = Format.Hex;
    private SD.Color _currentColor = SD.Color.Black;
    private int _currentX, _currentY;

    // 11x11 zoomed region around the cursor, rendered into the 164x164 image.
    private const int ZoomRadius = 5; // pixels each side => 11x11 sample

    public ColorPickerWindow()
    {
        InitializeComponent();
        WindowStartupLocation = WindowStartupLocation.Manual;
        Left = 0; Top = 0;
        Width  = SystemParameters.PrimaryScreenWidth;
        Height = SystemParameters.PrimaryScreenHeight;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        _screenBitmap = ScreenCapture.CapturePrimaryScreen(includeCursor: false);
        _screenSource = ScreenCapture.ToBitmapSource(_screenBitmap);
        BackgroundImage.Source = _screenSource;
        Focus();
    }

    protected override void OnClosed(EventArgs e)
    {
        _screenBitmap?.Dispose();
        base.OnClosed(e);
    }

    private void OnMouseMove(object sender, MouseEventArgs e)
    {
        var p = e.GetPosition(RootGrid);
        UpdateMagnifier(p);
    }

    private void OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        CommitColor();
    }

    private void OnMouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        CycleFormat();
        e.Handled = true;
    }

    private void OnKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape) Close();
        else if (e.Key == Key.F) CycleFormat();
        else if (e.Key == Key.Enter || e.Key == Key.Space) CommitColor();
    }

    private void CycleFormat()
    {
        _format = _format switch
        {
            Format.Hex => Format.Rgb,
            Format.Rgb => Format.Hsl,
            _          => Format.Hex,
        };
        UpdateReadouts();
    }

    // -------- Magnifier / color readout --------

    private void UpdateMagnifier(Point cursor)
    {
        if (_screenBitmap == null || _screenSource == null) return;

        // Convert DIP cursor → device pixel coordinates of the captured bitmap.
        var dpi = VisualTreeHelper.GetDpi(this);
        int px = (int)Math.Round(cursor.X * dpi.DpiScaleX);
        int py = (int)Math.Round(cursor.Y * dpi.DpiScaleY);
        px = Math.Clamp(px, 0, _screenBitmap.Width  - 1);
        py = Math.Clamp(py, 0, _screenBitmap.Height - 1);

        _currentX = px; _currentY = py;
        _currentColor = _screenBitmap.GetPixel(px, py);

        // Build a cropped 11x11 region for the zoom image (clamped to source bounds).
        int x = px - ZoomRadius, y = py - ZoomRadius;
        int w = ZoomRadius * 2 + 1, h = ZoomRadius * 2 + 1;
        if (x < 0) { w += x; x = 0; }
        if (y < 0) { h += y; y = 0; }
        if (x + w > _screenBitmap.Width)  w = _screenBitmap.Width  - x;
        if (y + h > _screenBitmap.Height) h = _screenBitmap.Height - y;
        if (w > 0 && h > 0)
        {
            ZoomImage.Source = new CroppedBitmap(_screenSource, new Int32Rect(x, y, w, h));
        }

        var wpfColor = Color.FromRgb(_currentColor.R, _currentColor.G, _currentColor.B);
        SwatchBorder.Background = new SolidColorBrush(wpfColor);
        UpdateReadouts();

        // Position the magnifier near the cursor, flipping around edges.
        Magnifier.Visibility = Visibility.Visible;
        Magnifier.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        double mw = Magnifier.DesiredSize.Width, mh = Magnifier.DesiredSize.Height;
        double offset = 22;
        double left = cursor.X + offset;
        double top  = cursor.Y + offset;
        if (left + mw > ActualWidth  - 8) left = cursor.X - mw - offset;
        if (top  + mh > ActualHeight - 8) top  = cursor.Y - mh - offset;
        if (left < 8) left = 8;
        if (top  < 8) top  = 8;
        Magnifier.Margin = new Thickness(left, top, 0, 0);
    }

    private void UpdateReadouts()
    {
        var c = _currentColor;
        string hex = $"#{c.R:X2}{c.G:X2}{c.B:X2}";
        string rgb = $"rgb({c.R}, {c.G}, {c.B})";
        string hsl = ToHsl(c);

        switch (_format)
        {
            case Format.Hex: HexText.Text = hex; SubText.Text = rgb; break;
            case Format.Rgb: HexText.Text = rgb; SubText.Text = hex; break;
            case Format.Hsl: HexText.Text = hsl; SubText.Text = hex; break;
        }
        CoordText.Text = $"{_currentX}, {_currentY}";
    }

    private static string ToHsl(SD.Color c)
    {
        double r = c.R / 255.0, g = c.G / 255.0, b = c.B / 255.0;
        double max = Math.Max(r, Math.Max(g, b));
        double min = Math.Min(r, Math.Min(g, b));
        double h = 0, s, l = (max + min) / 2;
        double d = max - min;
        if (d < 1e-9) { s = 0; }
        else
        {
            s = l > 0.5 ? d / (2 - max - min) : d / (max + min);
            if      (max == r) h = ((g - b) / d + (g < b ? 6 : 0));
            else if (max == g) h = ((b - r) / d + 2);
            else               h = ((r - g) / d + 4);
            h *= 60;
        }
        return $"hsl({h:0}, {s * 100:0}%, {l * 100:0}%)";
    }

    private void CommitColor()
    {
        string text = _format switch
        {
            Format.Hex => $"#{_currentColor.R:X2}{_currentColor.G:X2}{_currentColor.B:X2}",
            Format.Rgb => $"rgb({_currentColor.R}, {_currentColor.G}, {_currentColor.B})",
            Format.Hsl => ToHsl(_currentColor),
            _          => $"#{_currentColor.R:X2}{_currentColor.G:X2}{_currentColor.B:X2}",
        };
        try { Clipboard.SetText(text); } catch { /* clipboard may be locked */ }

        ((App)Application.Current).NotifyInfo("Color copied", $"{text} copied to clipboard.");
        Close();
    }
}
