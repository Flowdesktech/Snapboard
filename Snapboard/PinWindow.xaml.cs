using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using Snapboard.Helpers;
using Snapboard.Settings;

namespace Snapboard;

/// <summary>
/// Floating, always-on-top "pinned screenshot" window in the spirit of
/// Snipaste's pin feature — a capability ShareX famously lacks. Users pin a
/// capture to the screen, keep it visible over any other app, zoom with the
/// wheel, drag it anywhere, and dismiss it with Esc.
///
/// The window has no chrome. It drags on left-button-down anywhere on the
/// image, reveals a tiny toolbar on hover, and exposes a right-click menu for
/// copy / save / opacity / always-on-top toggling.
/// </summary>
public partial class PinWindow : Window
{
    private readonly BitmapSource _source;
    private readonly double _naturalWidth;
    private readonly double _naturalHeight;
    private double _zoom = 1.0;

    public PinWindow(BitmapSource source, Point topLeftInScreenPx)
    {
        _source = source;
        InitializeComponent();

        PinnedImage.Source = source;

        var dpi = VisualTreeHelper.GetDpi(this);
        _naturalWidth  = source.PixelWidth  / dpi.DpiScaleX;
        _naturalHeight = source.PixelHeight / dpi.DpiScaleY;

        PinnedImage.Width  = _naturalWidth;
        PinnedImage.Height = _naturalHeight;

        // Place the pin exactly where the selection was on the original screen
        // so the user perceives it as "sticking" where it was captured.
        WindowStartupLocation = WindowStartupLocation.Manual;
        Left = topLeftInScreenPx.X / dpi.DpiScaleX;
        Top  = topLeftInScreenPx.Y / dpi.DpiScaleY;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        Focus();
        DarkTitleBar.Apply(this);

        // Fade-in the pin for a subtle "just stuck to the screen" vibe.
        var fade = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(140));
        BeginAnimation(OpacityProperty, fade);

        MouseEnter += (_, _) => FadeHoverBar(1);
        MouseLeave += (_, _) => FadeHoverBar(0);
    }

    private void FadeHoverBar(double to)
    {
        HoverBar.BeginAnimation(OpacityProperty,
            new DoubleAnimation(to, TimeSpan.FromMilliseconds(140)));
    }

    // ---------- Drag to move ----------

    private void OnWindowMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Left && e.LeftButton == MouseButtonState.Pressed)
        {
            try { DragMove(); } catch { /* DragMove throws if already moving */ }
        }
    }

    // ---------- Zoom with mouse wheel ----------

    private void OnMouseWheel(object sender, MouseWheelEventArgs e)
    {
        double step = e.Delta > 0 ? 1.1 : 1 / 1.1;
        ApplyZoom(_zoom * step);
        e.Handled = true;
    }

    private void ApplyZoom(double newZoom)
    {
        _zoom = Math.Clamp(newZoom, 0.25, 4.0);
        PinnedImage.Width  = _naturalWidth  * _zoom;
        PinnedImage.Height = _naturalHeight * _zoom;
        ShowZoomToast();
    }

    private void ShowZoomToast()
    {
        ZoomText.Text = $"{Math.Round(_zoom * 100)}%";
        var sb = new Storyboard();
        var fadeIn  = new DoubleAnimation(1, TimeSpan.FromMilliseconds(90));
        var fadeOut = new DoubleAnimation(0, TimeSpan.FromMilliseconds(260))
        {
            BeginTime = TimeSpan.FromMilliseconds(650)
        };
        Storyboard.SetTarget(fadeIn,  ZoomToast);
        Storyboard.SetTarget(fadeOut, ZoomToast);
        Storyboard.SetTargetProperty(fadeIn,  new PropertyPath(OpacityProperty));
        Storyboard.SetTargetProperty(fadeOut, new PropertyPath(OpacityProperty));
        sb.Children.Add(fadeIn);
        sb.Children.Add(fadeOut);
        sb.Begin();
    }

    // ---------- Keyboard shortcuts ----------

    private void OnKeyDown(object sender, KeyEventArgs e)
    {
        bool ctrl = (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control;

        if (e.Key == Key.Escape) { Close(); e.Handled = true; return; }
        if (ctrl && e.Key == Key.C) { CopyToClipboard();            e.Handled = true; return; }
        if (ctrl && e.Key == Key.S) { SaveAs();                     e.Handled = true; return; }
        if (ctrl && e.Key == Key.D0) { ApplyZoom(1.0);              e.Handled = true; return; }
        if (ctrl && e.Key == Key.OemPlus) { ApplyZoom(_zoom * 1.1); e.Handled = true; return; }
        if (ctrl && e.Key == Key.OemMinus){ ApplyZoom(_zoom / 1.1); e.Handled = true; return; }
    }

    // ---------- Toolbar / menu handlers ----------

    private void OnCopyClick(object sender, RoutedEventArgs e) => CopyToClipboard();
    private void OnSaveClick(object sender, RoutedEventArgs e) => SaveAs();
    private void OnCloseClick(object sender, RoutedEventArgs e) => Close();

    private void OnResetZoomClick(object sender, RoutedEventArgs e) => ApplyZoom(1.0);

    private void OnAlwaysOnTopClick(object sender, RoutedEventArgs e)
    {
        if (sender is MenuItem mi) Topmost = mi.IsChecked;
    }

    private void OnOpacityClick(object sender, RoutedEventArgs e)
    {
        if (sender is MenuItem mi && mi.Tag is string s &&
            double.TryParse(s, System.Globalization.NumberStyles.Float,
                            System.Globalization.CultureInfo.InvariantCulture, out double op))
        {
            Opacity = Math.Clamp(op, 0.1, 1.0);
        }
    }

    // ---------- Actions ----------

    private void CopyToClipboard()
    {
        try { Clipboard.SetImage(_source); } catch { /* clipboard can be locked */ }
    }

    private void SaveAs()
    {
        var settings = ((App)Application.Current).Settings;
        bool isJpeg = settings.DefaultFormat.Equals("jpg", StringComparison.OrdinalIgnoreCase)
                   || settings.DefaultFormat.Equals("jpeg", StringComparison.OrdinalIgnoreCase);
        string ext = isJpeg ? ".jpg" : ".png";
        string fileName = BitmapSaver.BuildDefaultFileName(ext);

        string defaultDir = string.IsNullOrWhiteSpace(settings.SaveDirectory)
            ? Environment.GetFolderPath(Environment.SpecialFolder.MyPictures)
            : settings.SaveDirectory;

        var dlg = new SaveFileDialog
        {
            Filter = isJpeg
                ? "JPEG image (*.jpg)|*.jpg|PNG image (*.png)|*.png"
                : "PNG image (*.png)|*.png|JPEG image (*.jpg)|*.jpg",
            FileName = fileName,
            InitialDirectory = defaultDir
        };
        if (dlg.ShowDialog(this) != true) return;

        try
        {
            BitmapSaver.Save(_source, dlg.FileName, settings.JpegQuality);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Snapboard — save failed",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}
