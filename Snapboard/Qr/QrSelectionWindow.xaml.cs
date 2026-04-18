using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Snapboard.Helpers;
using SD = System.Drawing;

namespace Snapboard.Qr;

/// <summary>
/// Fullscreen drag-to-select overlay for QR code scanning. Mirrors
/// <c>OcrSelectionWindow</c> — same interaction model, same crop pipeline
/// — so users don't have to learn two different selectors. On drag end the
/// overlay closes itself and hands the cropped bitmap to
/// <see cref="App.StartQrFromBitmap"/>, which runs decoding on a
/// background thread.
/// </summary>
public partial class QrSelectionWindow : Window
{
    private enum State { Await, Selecting, Done }

    private SD.Bitmap? _screenBitmap;
    private State _state = State.Await;
    private Point _dragStart, _dragEnd;
    private Rect _selection;

    public QrSelectionWindow()
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
        BackgroundImage.Source = ScreenCapture.ToBitmapSource(_screenBitmap);
        UpdateDim(new Rect(0, 0, 0, 0));
        CursorHint.Visibility = Visibility.Visible;
        Focus();
        Keyboard.Focus(this);
    }

    protected override void OnClosed(EventArgs e)
    {
        _screenBitmap?.Dispose();
        base.OnClosed(e);
    }

    // -------- Input --------

    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            e.Handled = true;
            if (IsMouseCaptured) ReleaseMouseCapture();
            Close();
        }
    }

    private void OnMouseMove(object sender, MouseEventArgs e)
    {
        var p = e.GetPosition(RootGrid);
        if (_state == State.Await)
        {
            PositionCursorHint(p);
            return;
        }
        if (_state == State.Selecting)
        {
            _dragEnd = p;
            UpdateSelectionVisual();
        }
    }

    private void OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (_state != State.Await) return;
        _state = State.Selecting;
        _dragStart = _dragEnd = e.GetPosition(RootGrid);
        SelectionBorder.Visibility = Visibility.Visible;
        SizeLabel.Visibility = Visibility.Visible;
        CursorHint.Visibility = Visibility.Collapsed;
        UpdateSelectionVisual();
        CaptureMouse();
    }

    private void OnMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (_state != State.Selecting) return;
        _dragEnd = e.GetPosition(RootGrid);
        UpdateSelectionVisual();
        ReleaseMouseCapture();

        _selection = GetRect(_dragStart, _dragEnd);
        if (_selection.Width < 4 || _selection.Height < 4)
        {
            _state = State.Await;
            SelectionBorder.Visibility = Visibility.Collapsed;
            SizeLabel.Visibility = Visibility.Collapsed;
            CursorHint.Visibility = Visibility.Visible;
            UpdateDim(new Rect(0, 0, 0, 0));
            return;
        }

        _state = State.Done;

        // Same rule as OCR: do NOT open any window here. Crop, close the
        // overlay, hand bitmap to App which decodes on a background thread
        // and opens the result modal only on success. Desktop never locks.
        var crop = CropFromScreen(_selection);
        Close();
        if (crop == null) return;

        ((App)System.Windows.Application.Current).StartQrFromBitmap(crop);
    }

    // -------- Selection visuals --------

    private void UpdateSelectionVisual()
    {
        var r = GetRect(_dragStart, _dragEnd);
        SelectionBorder.Margin = new Thickness(r.X, r.Y, 0, 0);
        SelectionBorder.Width  = r.Width;
        SelectionBorder.Height = r.Height;
        SizeText.Text = $"{(int)r.Width} × {(int)r.Height}";
        SizeLabel.UpdateLayout();
        double lx = r.X;
        double ly = r.Y - SizeLabel.ActualHeight - 4;
        if (ly < 2) ly = r.Y + 4;
        SizeLabel.Margin = new Thickness(lx, ly, 0, 0);
        UpdateDim(r);
    }

    private void UpdateDim(Rect r)
    {
        double W = RootGrid.ActualWidth  > 0 ? RootGrid.ActualWidth  : Width;
        double H = RootGrid.ActualHeight > 0 ? RootGrid.ActualHeight : Height;

        if (r.Width == 0 || r.Height == 0)
        {
            DimTop.Width = W; DimTop.Height = H;
            Canvas.SetLeft(DimTop, 0); Canvas.SetTop(DimTop, 0);
            DimBottom.Width = 0; DimBottom.Height = 0;
            DimLeft.Width = 0;  DimLeft.Height = 0;
            DimRight.Width = 0; DimRight.Height = 0;
            return;
        }

        DimTop.Width = W; DimTop.Height = Math.Max(0, r.Y);
        Canvas.SetLeft(DimTop, 0); Canvas.SetTop(DimTop, 0);
        DimBottom.Width = W; DimBottom.Height = Math.Max(0, H - r.Bottom);
        Canvas.SetLeft(DimBottom, 0); Canvas.SetTop(DimBottom, r.Bottom);
        DimLeft.Width = Math.Max(0, r.X); DimLeft.Height = r.Height;
        Canvas.SetLeft(DimLeft, 0); Canvas.SetTop(DimLeft, r.Y);
        DimRight.Width = Math.Max(0, W - r.Right); DimRight.Height = r.Height;
        Canvas.SetLeft(DimRight, r.Right); Canvas.SetTop(DimRight, r.Y);
    }

    private static Rect GetRect(Point a, Point b)
    {
        double x = Math.Min(a.X, b.X);
        double y = Math.Min(a.Y, b.Y);
        double w = Math.Abs(a.X - b.X);
        double h = Math.Abs(a.Y - b.Y);
        return new Rect(x, y, w, h);
    }

    private void PositionCursorHint(Point cursor)
    {
        CursorHint.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        double w = CursorHint.DesiredSize.Width, h = CursorHint.DesiredSize.Height;
        double offset = 20;
        double left = cursor.X + offset, top = cursor.Y + offset;
        if (left + w > ActualWidth  - 6) left = cursor.X - w - offset;
        if (top  + h > ActualHeight - 6) top  = cursor.Y - h - offset;
        if (left < 6) left = 6;
        if (top  < 6) top  = 6;
        CursorHint.Margin = new Thickness(left, top, 0, 0);
    }

    // -------- Crop --------

    private SD.Bitmap? CropFromScreen(Rect selection)
    {
        if (_screenBitmap == null) return null;
        var dpi = VisualTreeHelper.GetDpi(this);
        int x = (int)Math.Round(selection.X * dpi.DpiScaleX);
        int y = (int)Math.Round(selection.Y * dpi.DpiScaleY);
        int w = (int)Math.Round(selection.Width  * dpi.DpiScaleX);
        int h = (int)Math.Round(selection.Height * dpi.DpiScaleY);
        x = Math.Clamp(x, 0, _screenBitmap.Width  - 1);
        y = Math.Clamp(y, 0, _screenBitmap.Height - 1);
        w = Math.Clamp(w, 1, _screenBitmap.Width  - x);
        h = Math.Clamp(h, 1, _screenBitmap.Height - y);
        return _screenBitmap.Clone(new SD.Rectangle(x, y, w, h), _screenBitmap.PixelFormat);
    }
}
