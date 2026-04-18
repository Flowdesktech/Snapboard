using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;

namespace Snapboard.Ruler;

public partial class PixelRulerWindow : Window
{
    private enum Orientation { Horizontal, Vertical }
    private Orientation _orientation = Orientation.Horizontal;

    /// <summary>
    /// How wide the resize hit-zone is at the far edge of the ruler
    /// (right edge for horizontal, bottom edge for vertical).
    /// </summary>
    private const double ResizeHandleThickness = 8;

    // ---- Visuals (reused for perf while resizing) ----
    private readonly List<Line> _ticks = new();
    private readonly List<TextBlock> _labels = new();
    private readonly Line _cursorIndicator = new()
    {
        Stroke = new SolidColorBrush(Color.FromRgb(0x3D, 0xA9, 0xFC)),
        StrokeThickness = 1,
        Visibility = Visibility.Collapsed,
    };
    private readonly TextBlock _cursorLabel = new()
    {
        Foreground = new SolidColorBrush(Color.FromRgb(0x3D, 0xA9, 0xFC)),
        FontFamily = new FontFamily("Consolas"),
        FontSize = 11,
        FontWeight = FontWeights.SemiBold,
        Visibility = Visibility.Collapsed,
    };

    private static readonly SolidColorBrush TickBrush  = new(Color.FromRgb(0x9B, 0xA1, 0xAD));
    private static readonly SolidColorBrush MinorBrush = new(Color.FromRgb(0x55, 0x5B, 0x68));
    private static readonly SolidColorBrush LabelBrush = new(Color.FromRgb(0xE6, 0xE8, 0xEC));

    // ---- Resize tracking ----
    private bool _resizing;
    private Point _resizeStartScreen;
    private double _resizeStartWidth;
    private double _resizeStartHeight;

    // ---- Dimensions tracked along "long" and "short" axes, not W/H.
    // This keeps orientation flips a pure transposition: rotating just
    // relabels which axis is long vs short, it never changes their values.
    private const double DefaultLong  = 900;
    private const double DefaultShort = 72;
    private const double LongMin      = 200;
    private const double ShortMin     =  40;
    private double _longAxis  = DefaultLong;
    private double _shortAxis = DefaultShort;

    // ---- Opacity submenu items, exposed so we can update their
    // IsChecked state when the menu reopens.
    private readonly List<MenuItem> _opacityItems = new();

    public PixelRulerWindow()
    {
        InitializeComponent();
        RulerCanvas.Children.Add(_cursorIndicator);
        RulerCanvas.Children.Add(_cursorLabel);
        ContextMenu = BuildContextMenu();
        Loaded += (_, _) =>
        {
            RedrawRuler();
            PositionGear();
        };
    }

    // -------- Mouse: drag-to-move / drag-to-resize / cursor indicator --------

    private void OnWindowMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        // Never hijack clicks on the gear — let it open the menu.
        if (e.OriginalSource is DependencyObject d && IsInsideGear(d)) return;

        var p = e.GetPosition(this);
        if (IsInResizeZone(p))
        {
            _resizing = true;
            _resizeStartScreen = PointToScreen(p);
            _resizeStartWidth  = Width;
            _resizeStartHeight = Height;
            CaptureMouse();
            e.Handled = true;
            return;
        }

        // Anywhere else on the ruler body = drag the whole window.
        try { DragMove(); } catch { /* can throw if mouse state got weird */ }
    }

    private void OnWindowMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (_resizing)
        {
            _resizing = false;
            ReleaseMouseCapture();
            e.Handled = true;
        }
    }

    private void OnWindowMouseMove(object sender, MouseEventArgs e)
    {
        var p = e.GetPosition(this);

        if (_resizing)
        {
            var cur = PointToScreen(p);
            double dx = cur.X - _resizeStartScreen.X;
            double dy = cur.Y - _resizeStartScreen.Y;

            if (_orientation == Orientation.Horizontal)
            {
                Width = Math.Max(MinWidth, _resizeStartWidth + dx);
                _longAxis = Width;
            }
            else
            {
                Height = Math.Max(MinHeight, _resizeStartHeight + dy);
                _longAxis = Height;
            }
            e.Handled = true;
            return;
        }

        // Not resizing — pick a cursor based on where we are, and move the
        // ruler's own cursor indicator so the user can read off a value.
        // Only the resize zone shows a resize cursor; the rest of the ruler
        // keeps the normal arrow (PicPick-style) so measuring feels natural.
        Cursor = IsInResizeZone(p)
            ? (_orientation == Orientation.Horizontal ? Cursors.SizeWE : Cursors.SizeNS)
            : Cursors.Arrow;

        MoveCursorIndicator(p);
    }

    private void OnWindowMouseLeave(object sender, MouseEventArgs e)
    {
        if (_resizing) return;
        _cursorIndicator.Visibility = Visibility.Collapsed;
        _cursorLabel.Visibility = Visibility.Collapsed;
    }

    private bool IsInResizeZone(Point p)
    {
        return _orientation == Orientation.Horizontal
            ? p.X >= ActualWidth - ResizeHandleThickness
            : p.Y >= ActualHeight - ResizeHandleThickness;
    }

    private static bool IsInsideGear(DependencyObject d)
    {
        while (d != null)
        {
            if (d is Button b && b.Name == "GearButton") return true;
            d = VisualTreeHelper.GetParent(d);
        }
        return false;
    }

    private void MoveCursorIndicator(Point p)
    {
        double W = RulerCanvas.ActualWidth;
        double H = RulerCanvas.ActualHeight;
        if (W <= 0 || H <= 0) return;

        if (_orientation == Orientation.Horizontal)
        {
            double x = Math.Clamp(p.X, 0, W);
            _cursorIndicator.X1 = _cursorIndicator.X2 = x + 0.5;
            _cursorIndicator.Y1 = 0;
            _cursorIndicator.Y2 = H;
            _cursorLabel.Text = $"{(int)x} px";
            Canvas.SetLeft(_cursorLabel, Math.Min(x + 4, W - 40));
            Canvas.SetTop(_cursorLabel, Math.Max(2, H - 14));
        }
        else
        {
            double y = Math.Clamp(p.Y, 0, H);
            _cursorIndicator.Y1 = _cursorIndicator.Y2 = y + 0.5;
            _cursorIndicator.X1 = 0;
            _cursorIndicator.X2 = W;
            _cursorLabel.Text = $"{(int)y} px";
            Canvas.SetLeft(_cursorLabel, Math.Max(2, W - 40));
            Canvas.SetTop(_cursorLabel, Math.Min(y + 4, H - 14));
        }
        _cursorIndicator.Visibility = Visibility.Visible;
        _cursorLabel.Visibility = Visibility.Visible;
    }

    // -------- Keyboard --------

    private void OnKeyDown(object sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Escape: Close(); break;
            case Key.H: SetOrientation(Orientation.Horizontal); break;
            case Key.V: SetOrientation(Orientation.Vertical); break;
            case Key.Space: ToggleOrientation(); break;
        }
    }

    // -------- Settings menu --------

    private ContextMenu BuildContextMenu()
    {
        var menu = new ContextMenu();

        // IMPORTANT: not IsCheckable — we manage IsChecked ourselves in the
        // Opened handler. IsCheckable auto-toggles on every click, which
        // fights our SetOrientation() logic and was making the ruler flip
        // sizes twice on one click.
        var hItem = new MenuItem { Header = "Horizontal" };
        hItem.Click += (_, _) => SetOrientation(Orientation.Horizontal);

        var vItem = new MenuItem { Header = "Vertical" };
        vItem.Click += (_, _) => SetOrientation(Orientation.Vertical);

        var topItem = new MenuItem { Header = "Always on top" };
        topItem.Click += (_, _) => Topmost = !Topmost;

        var resetItem = new MenuItem { Header = "Reset size" };
        resetItem.Click += (_, _) => ResetSize();

        var opacityHeader = new MenuItem { Header = "Opacity" };
        _opacityItems.Clear();
        foreach (var pct in new[] { 100, 90, 80, 65, 50, 35 })
        {
            var item = new MenuItem { Header = $"{pct}%", Tag = pct };
            double target = pct / 100.0;
            item.Click += (_, _) =>
            {
                Opacity = target;
                UpdateOpacityChecks();
            };
            _opacityItems.Add(item);
            opacityHeader.Items.Add(item);
        }

        var closeItem = new MenuItem { Header = "Close  (Esc)" };
        closeItem.Click += (_, _) => Close();

        var sepStyle = (Style)FindResource("DarkMenuSeparator");

        menu.Items.Add(hItem);
        menu.Items.Add(vItem);
        menu.Items.Add(new Separator { Style = sepStyle });
        menu.Items.Add(topItem);
        menu.Items.Add(opacityHeader);
        menu.Items.Add(resetItem);
        menu.Items.Add(new Separator { Style = sepStyle });
        menu.Items.Add(closeItem);

        menu.Opened += (_, _) =>
        {
            hItem.IsChecked   = _orientation == Orientation.Horizontal;
            vItem.IsChecked   = _orientation == Orientation.Vertical;
            topItem.IsChecked = Topmost;
            UpdateOpacityChecks();
        };

        return menu;
    }

    private void UpdateOpacityChecks()
    {
        foreach (var item in _opacityItems)
        {
            int pct = (int)item.Tag;
            item.IsChecked = Math.Abs(Opacity - pct / 100.0) < 0.02;
        }
    }

    private void OnGearClick(object sender, RoutedEventArgs e)
    {
        if (ContextMenu == null) return;
        ContextMenu.PlacementTarget = GearButton;
        ContextMenu.Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom;
        ContextMenu.IsOpen = true;
    }

    private void ToggleOrientation()
    {
        SetOrientation(_orientation == Orientation.Horizontal ? Orientation.Vertical : Orientation.Horizontal);
    }

    private void SetOrientation(Orientation orientation)
    {
        if (orientation == _orientation) return;

        // Capture the CURRENT axes before we flip, then reapply them on the
        // new orientation. The long axis stays long, short stays short —
        // orientation flips are a pure transposition of the same two numbers,
        // so no accidental doubling.
        CaptureAxes();
        _orientation = orientation;
        ApplyAxes();

        PositionGear();
        RedrawRuler();
    }

    private void ResetSize()
    {
        _longAxis  = DefaultLong;
        _shortAxis = DefaultShort;
        ApplyAxes();
    }

    /// <summary>Read Width/Height into _longAxis/_shortAxis based on the
    /// current orientation.</summary>
    private void CaptureAxes()
    {
        if (_orientation == Orientation.Horizontal)
        {
            _longAxis  = Width;
            _shortAxis = Height;
        }
        else
        {
            _longAxis  = Height;
            _shortAxis = Width;
        }
    }

    /// <summary>Write _longAxis/_shortAxis back into Width/Height based on
    /// the current orientation. MinWidth/MinHeight also swap so WPF doesn't
    /// silently clamp Width up to the horizontal MinWidth when we flip
    /// vertical (that was the "ruler is wider than expected" bug).</summary>
    private void ApplyAxes()
    {
        if (_orientation == Orientation.Horizontal)
        {
            MinWidth  = LongMin;
            MinHeight = ShortMin;
            Width  = _longAxis;
            Height = _shortAxis;
        }
        else
        {
            MinWidth  = ShortMin;
            MinHeight = LongMin;
            Width  = _shortAxis;
            Height = _longAxis;
        }
    }

    /// <summary>
    /// Horizontal ruler: gear sits at left edge, vertically centered.
    /// Vertical ruler: gear sits at top edge, horizontally centered.
    /// </summary>
    private void PositionGear()
    {
        if (_orientation == Orientation.Horizontal)
        {
            GearButton.HorizontalAlignment = HorizontalAlignment.Left;
            GearButton.VerticalAlignment   = VerticalAlignment.Center;
            GearButton.Margin              = new Thickness(3, 0, 0, 0);
        }
        else
        {
            GearButton.HorizontalAlignment = HorizontalAlignment.Center;
            GearButton.VerticalAlignment   = VerticalAlignment.Top;
            GearButton.Margin              = new Thickness(0, 3, 0, 0);
        }
    }

    // -------- Tick drawing --------

    private void OnCanvasSizeChanged(object sender, SizeChangedEventArgs e) => RedrawRuler();

    private void ClearTicks()
    {
        foreach (var l in _ticks)  RulerCanvas.Children.Remove(l);
        foreach (var t in _labels) RulerCanvas.Children.Remove(t);
        _ticks.Clear();
        _labels.Clear();
    }

    private void RedrawRuler()
    {
        ClearTicks();
        double W = RulerCanvas.ActualWidth;
        double H = RulerCanvas.ActualHeight;
        if (W <= 0 || H <= 0) return;

        double length    = _orientation == Orientation.Horizontal ? W : H;
        double thickness = _orientation == Orientation.Horizontal ? H : W;

        // PicPick-style tick lengths: short enough that they never dominate
        // the ruler body, leaving a clean lane for labels. Capped so a fat
        // ruler doesn't get comically long ticks either.
        double majorLen = Math.Min(thickness * 0.38, 22);
        double midLen   = Math.Min(thickness * 0.26, 15);
        double tenLen   = Math.Min(thickness * 0.16,  9);

        // Only tick every 10px — 5-px ticks produced a noisy "barcode" strip
        // on the vertical ruler in particular, which is exactly what the
        // user called out.
        for (int i = 10; i <= (int)length; i += 10)
        {
            double tickLen;
            Brush brush;
            if      (i % 100 == 0) { tickLen = majorLen; brush = TickBrush;  }
            else if (i %  50 == 0) { tickLen = midLen;   brush = TickBrush;  }
            else                   { tickLen = tenLen;   brush = MinorBrush; }

            var line = new Line
            {
                Stroke = brush,
                StrokeThickness = 1,
                SnapsToDevicePixels = true,
            };

            if (_orientation == Orientation.Horizontal)
            {
                line.X1 = line.X2 = i + 0.5;
                line.Y1 = 0;
                line.Y2 = tickLen;
            }
            else
            {
                line.Y1 = line.Y2 = i + 0.5;
                line.X1 = 0;
                line.X2 = tickLen;
            }

            RulerCanvas.Children.Add(line);
            _ticks.Add(line);

            if (i % 100 == 0)
            {
                var label = new TextBlock
                {
                    Text = i.ToString(),
                    Foreground = LabelBrush,
                    FontFamily = new FontFamily("Consolas"),
                    FontSize = 11,
                    FontWeight = FontWeights.SemiBold,
                };

                if (_orientation == Orientation.Horizontal)
                {
                    // Sit the label directly under the major tick, a hair to
                    // the right so the number reads as "starting" at the tick.
                    Canvas.SetLeft(label, i + 3);
                    Canvas.SetTop(label, majorLen + 2);
                }
                else
                {
                    // Center the label in the lane between the tick end and
                    // the far edge of the ruler, and vertically-center it on
                    // the tick line itself — that's the PicPick layout.
                    double laneStart = majorLen + 2;
                    double laneWidth = Math.Max(0, thickness - laneStart - 2);
                    label.TextAlignment = TextAlignment.Center;
                    label.Width = laneWidth;
                    Canvas.SetLeft(label, laneStart);
                    Canvas.SetTop(label, i - 8);
                }

                RulerCanvas.Children.Add(label);
                _labels.Add(label);
            }
        }
    }
}
