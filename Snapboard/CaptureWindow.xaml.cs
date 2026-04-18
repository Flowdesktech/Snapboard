using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using Snapboard.Helpers;
using Snapboard.Settings;
using Microsoft.Win32;
using SD = System.Drawing;
using WF = System.Windows.Forms;

namespace Snapboard;

public partial class CaptureWindow : Window
{
    private enum State { AwaitSelection, Selecting, Editing }
    private enum Tool { None, Pen, Rect, Arrow, Text, Blur }
    private enum ResizeHandle { None, N, NE, E, SE, S, SW, W, NW }

    // Captured screen data
    private SD.Bitmap? _screenBitmap;
    private SD.Bitmap? _blurredBitmap;
    private BitmapSource? _blurredSource;

    // DIP-space state
    private State _state = State.AwaitSelection;
    private Tool _currentTool = Tool.None;
    private Point _dragStart;
    private Point _dragEnd;
    private Rect _selection;

    // Tool settings
    private Color _strokeColor = Color.FromRgb(0xEF, 0x44, 0x44);
    private Brush _strokeBrush = new SolidColorBrush(Color.FromRgb(0xEF, 0x44, 0x44));
    private double _strokeThickness = 4;

    // 8 corner + midpoint handles on the selection (Lightshot style).
    // These are rendered as visual hints; hit-testing for resize uses geometry
    // around the selection edges so resizing still works even though the handle
    // rectangles themselves are IsHitTestVisible=false.
    private readonly List<Rectangle> _handles = new();

    // Selection resize / reselection interaction state.
    private ResizeHandle _activeResizeHandle = ResizeHandle.None;
    private Rect _resizeStartRect;
    private Point _resizeStartPoint;
    private bool _isResizingSelection;
    private bool _reselectInProgress;
    private Rect _selectionSnapshotBeforeReselect;

    // In-progress drawing
    private Polyline? _currentPen;
    private Shape? _currentShape;
    private Rectangle? _currentBlurRect;

    // Undo stack (UI elements added to AnnotationCanvas)
    private readonly Stack<UIElement> _history = new();

    public CaptureWindow()
    {
        InitializeComponent();
        WindowStartupLocation = WindowStartupLocation.Manual;
        Left = 0;
        Top = 0;
        Width = SystemParameters.PrimaryScreenWidth;
        Height = SystemParameters.PrimaryScreenHeight;

        // Realign once toolbars get a non-zero arranged size (important after
        // reselection when they were previously Collapsed).
        VToolbar.SizeChanged += OnToolbarSizeChanged;
        HToolbar.SizeChanged += OnToolbarSizeChanged;
    }

    // ---------- Lifecycle ----------

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        var settings = ((App)Application.Current).Settings;
        _screenBitmap = ScreenCapture.CapturePrimaryScreen(includeCursor: settings.CaptureCursor);
        BackgroundImage.Source = ScreenCapture.ToBitmapSource(_screenBitmap);

        // Pre-render a blurred version for the Blur tool.
        _blurredBitmap = BlurHelper.CreateBlurred(_screenBitmap, strength: 10);
        _blurredSource = ScreenCapture.ToBitmapSource(_blurredBitmap);

        // Start with the whole viewport dimmed
        UpdateDim(new Rect(0, 0, 0, 0));
        CursorHint.Visibility = Visibility.Visible;
        Focus();
    }

    protected override void OnClosed(EventArgs e)
    {
        _screenBitmap?.Dispose();
        _blurredBitmap?.Dispose();
        base.OnClosed(e);
    }

    // ---------- Input ----------

    private void OnPreviewKeyDown(object sender, KeyEventArgs e) => HandleGlobalHotkeys(e);

    private void OnKeyDown(object sender, KeyEventArgs e)
    {
        // Keep a regular KeyDown hook as a fallback, but let PreviewKeyDown
        // be the primary path so shortcuts still work when inner controls
        // (buttons/textbox/popups) have keyboard focus.
        if (!e.Handled) HandleGlobalHotkeys(e);
    }

    private void HandleGlobalHotkeys(KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            Close();
            e.Handled = true;
            return;
        }
        if (e.Key == Key.Z && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
        {
            Undo();
            e.Handled = true;
            return;
        }
        if (e.Key == Key.S && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
        {
            Save();
            e.Handled = true;
            return;
        }
        if ((e.Key == Key.C && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
            || e.Key == Key.Enter)
        {
            CopyToClipboard();
            e.Handled = true;
            return;
        }
    }

    private void OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        var p = ClampToViewport(e.GetPosition(RootGrid));

        if (_state == State.AwaitSelection)
        {
            BeginSelectionDrag(p, reselectFromEditing: false);
            e.Handled = true;
            return;
        }

        if (_state == State.Editing)
        {
            // 1) Resize takes precedence over tool drawing when pointer is on
            // an edge/corner handle zone.
            var handle = HitTestResizeHandle(p);
            if (handle != ResizeHandle.None)
            {
                _activeResizeHandle = handle;
                _resizeStartPoint = p;
                _resizeStartRect = _selection;
                _isResizingSelection = true;
                Cursor = CursorForHandle(handle);
                CaptureMouse();
                e.Handled = true;
                return;
            }

            bool hasDrawingTool = _currentTool != Tool.None;
            bool insideSelection = _selection.Contains(p);

            // 2) Allow re-selecting by drag while already in editing:
            //    - if pointer starts outside the current selection, OR
            //    - if no drawing tool is active (so drag is interpreted as
            //      "new selection" instead of "draw stroke").
            if (!insideSelection || !hasDrawingTool)
            {
                BeginSelectionDrag(p, reselectFromEditing: true);
                e.Handled = true;
                return;
            }

            switch (_currentTool)
            {
                case Tool.Pen:
                    _currentPen = new Polyline
                    {
                        Stroke = _strokeBrush,
                        StrokeThickness = _strokeThickness,
                        StrokeLineJoin = PenLineJoin.Round,
                        StrokeStartLineCap = PenLineCap.Round,
                        StrokeEndLineCap = PenLineCap.Round,
                        Points = new PointCollection { p }
                    };
                    AnnotationCanvas.Children.Add(_currentPen);
                    CaptureMouse();
                    break;
                case Tool.Rect:
                    _currentShape = new Rectangle
                    {
                        Stroke = _strokeBrush,
                        StrokeThickness = _strokeThickness,
                        Fill = Brushes.Transparent
                    };
                    Canvas.SetLeft(_currentShape, p.X);
                    Canvas.SetTop(_currentShape, p.Y);
                    PreviewCanvas.Children.Add(_currentShape);
                    _dragStart = p;
                    CaptureMouse();
                    break;
                case Tool.Arrow:
                    _dragStart = p;
                    var arrow = new Path
                    {
                        Stroke = _strokeBrush,
                        StrokeThickness = _strokeThickness,
                        Fill = _strokeBrush,
                        StrokeLineJoin = PenLineJoin.Round
                    };
                    _currentShape = arrow;
                    PreviewCanvas.Children.Add(arrow);
                    CaptureMouse();
                    break;
                case Tool.Blur:
                    _currentBlurRect = new Rectangle
                    {
                        Stroke = Brushes.White,
                        StrokeThickness = 1,
                        StrokeDashArray = new DoubleCollection { 4, 2 },
                        Fill = new SolidColorBrush(Color.FromArgb(40, 255, 255, 255))
                    };
                    Canvas.SetLeft(_currentBlurRect, p.X);
                    Canvas.SetTop(_currentBlurRect, p.Y);
                    PreviewCanvas.Children.Add(_currentBlurRect);
                    _dragStart = p;
                    CaptureMouse();
                    break;
                case Tool.Text:
                    BeginText(p);
                    break;
            }
            e.Handled = true;
        }
    }

    private void OnMouseMove(object sender, MouseEventArgs e)
    {
        var p = ClampToViewport(e.GetPosition(RootGrid));

        if (_state == State.AwaitSelection)
        {
            PositionCursorHint(p);
            return;
        }

        if (_state == State.Selecting)
        {
            _dragEnd = p;
            UpdateSelectionVisual();
            return;
        }

        if (_state != State.Editing) return;

        if (_isResizingSelection && e.LeftButton == MouseButtonState.Pressed)
        {
            ResizeSelectionFromHandle(p);
            return;
        }

        if (e.LeftButton != MouseButtonState.Pressed)
        {
            UpdateEditingCursor(p);
            return;
        }

        var cp = ClampToSelection(p);

        switch (_currentTool)
        {
            case Tool.Pen when _currentPen != null:
                _currentPen.Points.Add(cp);
                break;
            case Tool.Rect when _currentShape != null:
                UpdateRectDrag(_currentShape, _dragStart, cp);
                break;
            case Tool.Arrow when _currentShape is Path path:
                path.Data = BuildArrowGeometry(_dragStart, cp, _strokeThickness);
                break;
            case Tool.Blur when _currentBlurRect != null:
                UpdateRectDrag(_currentBlurRect, _dragStart, cp);
                break;
        }
    }

    private void OnMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        var p = ClampToViewport(e.GetPosition(RootGrid));

        if (_state == State.Selecting)
        {
            _dragEnd = p;
            UpdateSelectionVisual();
            ReleaseMouseCapture();

            _selection = GetRect(_dragStart, _dragEnd);
            if (_selection.Width < 4 || _selection.Height < 4)
            {
                // Too small:
                // - if this was a reselect attempt, restore the old selection;
                // - otherwise reset to idle.
                if (_reselectInProgress)
                {
                    _selection = _selectionSnapshotBeforeReselect;
                    _reselectInProgress = false;
                    EnterEditingState();
                }
                else
                {
                    SelectionBorder.Visibility = Visibility.Collapsed;
                    SizeLabel.Visibility = Visibility.Collapsed;
                    CursorHint.Visibility = Visibility.Visible;
                    _state = State.AwaitSelection;
                }
                return;
            }

            if (_reselectInProgress)
            {
                // New selection committed from editing mode:
                // wipe old annotations/history so the new region starts clean.
                _reselectInProgress = false;
                AnnotationCanvas.Children.Clear();
                PreviewCanvas.Children.Clear();
                _history.Clear();
                _currentPen = null;
                _currentShape = null;
                _currentBlurRect = null;
            }

            EnterEditingState();
            return;
        }

        if (_state != State.Editing) return;

        if (_isResizingSelection)
        {
            _isResizingSelection = false;
            _activeResizeHandle = ResizeHandle.None;
            ReleaseMouseCapture();
            UpdateEditingCursor(p);
            e.Handled = true;
            return;
        }

        ReleaseMouseCapture();

        switch (_currentTool)
        {
            case Tool.Pen when _currentPen != null:
                _history.Push(_currentPen);
                _currentPen = null;
                break;
            case Tool.Rect when _currentShape != null:
                PreviewCanvas.Children.Remove(_currentShape);
                AnnotationCanvas.Children.Add(_currentShape);
                _history.Push(_currentShape);
                _currentShape = null;
                break;
            case Tool.Arrow when _currentShape != null:
                PreviewCanvas.Children.Remove(_currentShape);
                AnnotationCanvas.Children.Add(_currentShape);
                _history.Push(_currentShape);
                _currentShape = null;
                break;
            case Tool.Blur when _currentBlurRect != null:
                PreviewCanvas.Children.Remove(_currentBlurRect);
                CommitBlur(_currentBlurRect);
                _currentBlurRect = null;
                break;
        }
    }

    // ---------- Selection ----------

    private void EnterEditingState()
    {
        _state = State.Editing;
        _isResizingSelection = false;
        _activeResizeHandle = ResizeHandle.None;
        _reselectInProgress = false;

        AnnotationCanvas.Visibility = Visibility.Visible;
        PreviewCanvas.Visibility = Visibility.Visible;
        HandleCanvas.Visibility = Visibility.Visible;
        CursorHint.Visibility = Visibility.Collapsed;
        SelectionBorder.Visibility = Visibility.Visible;
        SizeLabel.Visibility = Visibility.Visible;

        var clip = new RectangleGeometry(_selection);
        clip.Freeze();
        AnnotationCanvas.Clip = clip;
        PreviewCanvas.Clip = clip;

        // Position bars while they're Hidden (participates in layout but
        // doesn't flash). A SizeChanged callback does one final alignment once
        // their arranged size is definitely non-zero after reselection.
        VToolbar.Visibility = Visibility.Hidden;
        HToolbar.Visibility = Visibility.Hidden;
        RootGrid.UpdateLayout();

        PositionToolbars();
        UpdateColorSwatch();
        UpdateSizeSwatch();
        RenderHandles();

        VToolbar.Visibility = Visibility.Visible;
        HToolbar.Visibility = Visibility.Visible;

        UpdateEditingCursor(Mouse.GetPosition(RootGrid));
    }

    private void OnToolbarSizeChanged(object? sender, SizeChangedEventArgs e)
    {
        if (_state != State.Editing) return;
        if (VToolbar.Visibility != Visibility.Visible || HToolbar.Visibility != Visibility.Visible) return;
        if (VToolbar.ActualWidth <= 0 || VToolbar.ActualHeight <= 0) return;
        if (HToolbar.ActualWidth <= 0 || HToolbar.ActualHeight <= 0) return;

        // Re-align on the next render tick so both bars have settled arranged
        // sizes before placement math runs.
        Dispatcher.BeginInvoke(new Action(() =>
        {
            if (_state != State.Editing) return;
            if (VToolbar.Visibility != Visibility.Visible || HToolbar.Visibility != Visibility.Visible) return;
            PositionToolbars();
        }), System.Windows.Threading.DispatcherPriority.Render);
    }

    private void BeginSelectionDrag(Point start, bool reselectFromEditing)
    {
        if (reselectFromEditing)
        {
            _reselectInProgress = true;
            _selectionSnapshotBeforeReselect = _selection;

            // Hide editing chrome while drawing a fresh selection rectangle.
            ColorPopup.IsOpen = false;
            SizePopup.IsOpen = false;
            VToolbar.Visibility = Visibility.Collapsed;
            HToolbar.Visibility = Visibility.Collapsed;
            HandleCanvas.Visibility = Visibility.Collapsed;
            AnnotationCanvas.Visibility = Visibility.Collapsed;
            PreviewCanvas.Visibility = Visibility.Collapsed;
        }
        else
        {
            _reselectInProgress = false;
        }

        _state = State.Selecting;
        _dragStart = start;
        _dragEnd = start;
        Cursor = Cursors.Cross;
        SelectionBorder.Visibility = Visibility.Visible;
        SizeLabel.Visibility = Visibility.Visible;
        CursorHint.Visibility = Visibility.Collapsed;
        UpdateSelectionVisual();
        CaptureMouse();
    }

    private Point ClampToViewport(Point p)
    {
        double w = RootGrid.ActualWidth > 0 ? RootGrid.ActualWidth : ActualWidth;
        double h = RootGrid.ActualHeight > 0 ? RootGrid.ActualHeight : ActualHeight;
        if (w <= 0 || h <= 0) return p;

        return new Point(
            Math.Clamp(p.X, 0, w),
            Math.Clamp(p.Y, 0, h));
    }

    private ResizeHandle HitTestResizeHandle(Point p)
    {
        if (_selection.Width <= 0 || _selection.Height <= 0) return ResizeHandle.None;

        const double grip = 8.0;
        bool nearLeft = Math.Abs(p.X - _selection.Left) <= grip;
        bool nearRight = Math.Abs(p.X - _selection.Right) <= grip;
        bool nearTop = Math.Abs(p.Y - _selection.Top) <= grip;
        bool nearBottom = Math.Abs(p.Y - _selection.Bottom) <= grip;
        bool withinX = p.X >= _selection.Left - grip && p.X <= _selection.Right + grip;
        bool withinY = p.Y >= _selection.Top - grip && p.Y <= _selection.Bottom + grip;

        if (!withinX || !withinY) return ResizeHandle.None;

        if (nearTop && nearLeft) return ResizeHandle.NW;
        if (nearTop && nearRight) return ResizeHandle.NE;
        if (nearBottom && nearLeft) return ResizeHandle.SW;
        if (nearBottom && nearRight) return ResizeHandle.SE;
        if (nearTop) return ResizeHandle.N;
        if (nearBottom) return ResizeHandle.S;
        if (nearLeft) return ResizeHandle.W;
        if (nearRight) return ResizeHandle.E;

        return ResizeHandle.None;
    }

    private static Cursor CursorForHandle(ResizeHandle h) => h switch
    {
        ResizeHandle.N or ResizeHandle.S => Cursors.SizeNS,
        ResizeHandle.E or ResizeHandle.W => Cursors.SizeWE,
        ResizeHandle.NE or ResizeHandle.SW => Cursors.SizeNESW,
        ResizeHandle.NW or ResizeHandle.SE => Cursors.SizeNWSE,
        _ => Cursors.Arrow,
    };

    private void UpdateEditingCursor(Point pointer)
    {
        var handle = HitTestResizeHandle(pointer);
        if (handle != ResizeHandle.None)
        {
            Cursor = CursorForHandle(handle);
            return;
        }

        Cursor = _currentTool switch
        {
            Tool.Text => Cursors.IBeam,
            Tool.None => Cursors.Arrow,
            _ => Cursors.Cross,
        };
    }

    private void ResizeSelectionFromHandle(Point current)
    {
        if (_activeResizeHandle == ResizeHandle.None) return;

        const double minSize = 8;
        var viewport = new Rect(0, 0,
            RootGrid.ActualWidth > 0 ? RootGrid.ActualWidth : ActualWidth,
            RootGrid.ActualHeight > 0 ? RootGrid.ActualHeight : ActualHeight);

        double left = _resizeStartRect.Left;
        double top = _resizeStartRect.Top;
        double right = _resizeStartRect.Right;
        double bottom = _resizeStartRect.Bottom;
        double dx = current.X - _resizeStartPoint.X;
        double dy = current.Y - _resizeStartPoint.Y;

        switch (_activeResizeHandle)
        {
            case ResizeHandle.N:
                top += dy;
                break;
            case ResizeHandle.NE:
                top += dy;
                right += dx;
                break;
            case ResizeHandle.E:
                right += dx;
                break;
            case ResizeHandle.SE:
                right += dx;
                bottom += dy;
                break;
            case ResizeHandle.S:
                bottom += dy;
                break;
            case ResizeHandle.SW:
                bottom += dy;
                left += dx;
                break;
            case ResizeHandle.W:
                left += dx;
                break;
            case ResizeHandle.NW:
                top += dy;
                left += dx;
                break;
        }

        left = Math.Clamp(left, viewport.Left, viewport.Right);
        right = Math.Clamp(right, viewport.Left, viewport.Right);
        top = Math.Clamp(top, viewport.Top, viewport.Bottom);
        bottom = Math.Clamp(bottom, viewport.Top, viewport.Bottom);

        bool adjustsLeft = _activeResizeHandle is ResizeHandle.W or ResizeHandle.NW or ResizeHandle.SW;
        bool adjustsTop = _activeResizeHandle is ResizeHandle.N or ResizeHandle.NE or ResizeHandle.NW;

        if (right - left < minSize)
        {
            if (adjustsLeft) left = right - minSize;
            else right = left + minSize;
        }
        if (bottom - top < minSize)
        {
            if (adjustsTop) top = bottom - minSize;
            else bottom = top + minSize;
        }

        left = Math.Clamp(left, viewport.Left, viewport.Right - minSize);
        top = Math.Clamp(top, viewport.Top, viewport.Bottom - minSize);
        right = Math.Clamp(right, left + minSize, viewport.Right);
        bottom = Math.Clamp(bottom, top + minSize, viewport.Bottom);

        _selection = new Rect(new Point(left, top), new Point(right, bottom));
        UpdateEditingSelectionVisual();
    }

    private void UpdateEditingSelectionVisual()
    {
        SelectionBorder.Margin = new Thickness(_selection.X, _selection.Y, 0, 0);
        SelectionBorder.Width = _selection.Width;
        SelectionBorder.Height = _selection.Height;

        SizeText.Text = $"{(int)_selection.Width} × {(int)_selection.Height}";
        SizeLabel.UpdateLayout();
        double lx = _selection.X;
        double ly = _selection.Y - SizeLabel.ActualHeight - 4;
        if (ly < 2) ly = _selection.Y + 4;
        SizeLabel.Margin = new Thickness(lx, ly, 0, 0);

        UpdateDim(_selection);

        AnnotationCanvas.Clip = new RectangleGeometry(_selection);
        PreviewCanvas.Clip = new RectangleGeometry(_selection);

        RenderHandles();
        PositionToolbars();
    }

    /// <summary>
    /// Classic Lightshot toolbar layout:
    ///
    ///                       ┌──────────────┐ ┐
    ///                       │              │ │
    ///                       │  selection   │ │  ┌─┐
    ///                       │              │ │  │ │  ← Vertical rail,
    ///                       │              │ │  │ │    outside-right,
    ///                       └──────────────┘ ┘  └─┘    BOTTOM-aligned with
    ///                                ┌─────────────┐   the selection.
    ///                                │ H action bar│ ← Below selection,
    ///                                └─────────────┘   RIGHT edge aligned
    ///                                             ↑    with the SELECTION's
    ///                                             │    right edge.
    ///
    ///  * Vertical tool rail sits just outside the right edge and is
    ///    anchored to the selection's BOTTOM — we compute vBottom first
    ///    (= selection.Bottom, clamped into the viewport) and derive
    ///    vTop from it. Falls back to inside-right or outside-left when
    ///    pressed against the right screen edge.
    ///  * Horizontal action bar sits just below the selection with its
    ///    RIGHT edge aligned with the selection's right edge (Lightshot).
    ///    We compute hRight first (= selection.Right, clamped) and derive
    ///    hLeft from it. The bar flips above the selection when there's
    ///    no room below.
    /// </summary>
    private void PositionToolbars()
    {
        const double gap = 2;
        const double margin = 4;

        VToolbar.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        HToolbar.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));

        double vW = VToolbar.DesiredSize.Width;
        double vH = VToolbar.DesiredSize.Height;
        double hW = HToolbar.DesiredSize.Width;
        double hH = HToolbar.DesiredSize.Height;

        // When the bars were previously Collapsed (e.g. during a re-select),
        // DesiredSize can occasionally come back as 0 on the first measure
        // pass. If we position with zero sizes, the next visible layout makes
        // the bars look offset/misaligned. Force one more layout+measure pass,
        // then fall back to ActualSize to guarantee non-zero geometry.
        if (vW <= 0 || vH <= 0 || hW <= 0 || hH <= 0)
        {
            RootGrid.UpdateLayout();
            VToolbar.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            HToolbar.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));

            vW = VToolbar.DesiredSize.Width;
            vH = VToolbar.DesiredSize.Height;
            hW = HToolbar.DesiredSize.Width;
            hH = HToolbar.DesiredSize.Height;

            if (vW <= 0) vW = VToolbar.ActualWidth;
            if (vH <= 0) vH = VToolbar.ActualHeight;
            if (hW <= 0) hW = HToolbar.ActualWidth;
            if (hH <= 0) hH = HToolbar.ActualHeight;
        }

        // --- Vertical rail: outside-right → inside-right → outside-left ---
        double vLeft = _selection.Right + gap;
        if (vLeft + vW > ActualWidth - margin)
        {
            vLeft = _selection.Right - vW - gap;         // inside-right
            if (vLeft < _selection.Left + gap)
                vLeft = _selection.Left - vW - gap;      // outside-left
        }
        vLeft = Math.Clamp(vLeft, margin, Math.Max(margin, ActualWidth - vW - margin));

        // Anchor the rail to the selection's BOTTOM edge (classic Lightshot:
        // the rail grows upward from that corner). We derive vTop from vBottom
        // so the bottom stays pinned no matter how tall the rail is, then
        // clamp so a tall rail on a tiny selection doesn't spill off the top
        // or a rail whose bottom is already off-screen gets pulled back in.
        double vBottom = _selection.Bottom;
        if (vBottom > ActualHeight - margin) vBottom = ActualHeight - margin;
        if (vBottom - vH < margin)           vBottom = margin + vH;
        double vTop = vBottom - vH;

        VToolbar.Margin = new Thickness(vLeft, vTop, 0, 0);

        // --- Horizontal action bar: below selection, right-aligned with the
        // selection box (Lightshot). Use right-anchor math so the bar keeps
        // that edge unless clamping is required near viewport bounds.
        double hRight = _selection.Right;
        if (hRight > ActualWidth - margin) hRight = ActualWidth - margin;
        if (hRight - hW < margin)          hRight = margin + hW;
        double hLeft = hRight - hW;

        double hTop = _selection.Bottom + gap;
        if (hTop + hH > ActualHeight - margin) hTop = _selection.Top - hH - gap;
        if (hTop < margin) hTop = margin;

        HToolbar.Margin = new Thickness(hLeft, hTop, 0, 0);
    }

    /// <summary>Render the 8 selection handles at corners + midpoints.
    /// Hit-testing for resize is geometry-based (not per-rectangle), so the
    /// handles can stay non-interactive visuals while resize still works.</summary>
    private void RenderHandles()
    {
        foreach (var h in _handles) HandleCanvas.Children.Remove(h);
        _handles.Clear();

        if (_selection.Width <= 0 || _selection.Height <= 0) return;

        double s = _selection.Left;
        double t = _selection.Top;
        double r = _selection.Right;
        double b = _selection.Bottom;
        double cx = s + _selection.Width / 2.0;
        double cy = t + _selection.Height / 2.0;

        foreach (var (x, y) in new (double, double)[]
        {
            (s, t), (cx, t), (r, t),
            (s, cy),         (r, cy),
            (s, b), (cx, b), (r, b),
        })
        {
            var dot = new Rectangle
            {
                Width = 7,
                Height = 7,
                Fill = System.Windows.Media.Brushes.White,
                Stroke = new SolidColorBrush(Color.FromRgb(0x3D, 0xA9, 0xFC)),
                StrokeThickness = 1,
                SnapsToDevicePixels = true,
                IsHitTestVisible = false,
            };
            Canvas.SetLeft(dot, x - 3.5);
            Canvas.SetTop(dot, y - 3.5);
            HandleCanvas.Children.Add(dot);
            _handles.Add(dot);
        }
    }

    private void UpdateColorSwatch()
    {
        ColorSwatch.Background = new SolidColorBrush(_strokeColor);
    }

    private void UpdateSizeSwatch()
    {
        double d = Math.Clamp(_strokeThickness + 4, 6, 16);
        SizeSwatch.Width = d;
        SizeSwatch.Height = d;
    }

    private void UpdateSelectionVisual()
    {
        var r = GetRect(_dragStart, _dragEnd);

        SelectionBorder.Margin = new Thickness(r.X, r.Y, 0, 0);
        SelectionBorder.Width = r.Width;
        SelectionBorder.Height = r.Height;

        SizeText.Text = $"{(int)r.Width} × {(int)r.Height}";
        SizeLabel.UpdateLayout();
        double lx = r.X;
        double ly = r.Y - SizeLabel.ActualHeight - 4;
        if (ly < 2) ly = r.Y + 4; // flip inside if no room above
        SizeLabel.Margin = new Thickness(lx, ly, 0, 0);

        UpdateDim(r);
    }

    private void UpdateDim(Rect r)
    {
        double W = RootGrid.ActualWidth > 0 ? RootGrid.ActualWidth : Width;
        double H = RootGrid.ActualHeight > 0 ? RootGrid.ActualHeight : Height;

        if (r.Width == 0 || r.Height == 0)
        {
            DimTop.Width = W; DimTop.Height = H;
            Canvas.SetLeft(DimTop, 0); Canvas.SetTop(DimTop, 0);
            DimBottom.Width = 0; DimBottom.Height = 0;
            DimLeft.Width = 0; DimLeft.Height = 0;
            DimRight.Width = 0; DimRight.Height = 0;
            return;
        }

        // Top strip
        DimTop.Width = W; DimTop.Height = Math.Max(0, r.Y);
        Canvas.SetLeft(DimTop, 0); Canvas.SetTop(DimTop, 0);
        // Bottom strip
        DimBottom.Width = W; DimBottom.Height = Math.Max(0, H - r.Bottom);
        Canvas.SetLeft(DimBottom, 0); Canvas.SetTop(DimBottom, r.Bottom);
        // Left strip
        DimLeft.Width = Math.Max(0, r.X); DimLeft.Height = r.Height;
        Canvas.SetLeft(DimLeft, 0); Canvas.SetTop(DimLeft, r.Y);
        // Right strip
        DimRight.Width = Math.Max(0, W - r.Right); DimRight.Height = r.Height;
        Canvas.SetLeft(DimRight, r.Right); Canvas.SetTop(DimRight, r.Y);
    }

    private Point ClampToSelection(Point p)
    {
        double x = Math.Max(_selection.Left, Math.Min(_selection.Right, p.X));
        double y = Math.Max(_selection.Top, Math.Min(_selection.Bottom, p.Y));
        return new Point(x, y);
    }

    private static Rect GetRect(Point a, Point b)
    {
        double x = Math.Min(a.X, b.X);
        double y = Math.Min(a.Y, b.Y);
        double w = Math.Abs(a.X - b.X);
        double h = Math.Abs(a.Y - b.Y);
        return new Rect(x, y, w, h);
    }

    private static void UpdateRectDrag(Shape s, Point a, Point b)
    {
        var r = GetRect(a, b);
        Canvas.SetLeft(s, r.X);
        Canvas.SetTop(s, r.Y);
        s.Width = r.Width;
        s.Height = r.Height;
    }

    // ---------- Tools (toolbar handlers) ----------

    private void OnToolSelected(object sender, RoutedEventArgs e)
    {
        if (sender is not ToggleButton tb) return;
        foreach (var other in new[] { ToolPen, ToolRect, ToolArrow, ToolText, ToolBlur })
        {
            if (!ReferenceEquals(other, tb)) other.IsChecked = false;
        }

        if (tb.IsChecked != true)
        {
            _currentTool = Tool.None;
            AnnotationCanvas.IsHitTestVisible = false;
            return;
        }

        _currentTool = tb.Tag switch
        {
            "Pen"   => Tool.Pen,
            "Rect"  => Tool.Rect,
            "Arrow" => Tool.Arrow,
            "Text"  => Tool.Text,
            "Blur"  => Tool.Blur,
            _       => Tool.None
        };

        // Put a hit-testable transparent overlay inside the selection so the
        // mouse events hit the window (which handles drawing).
        AnnotationCanvas.IsHitTestVisible = false; // window handles events directly
        if (_state == State.Editing) UpdateEditingCursor(Mouse.GetPosition(RootGrid));
    }

    private void OnColorButtonClick(object sender, RoutedEventArgs e)
    {
        SizePopup.IsOpen = false;
        ColorPopup.IsOpen = !ColorPopup.IsOpen;
    }

    private void OnSizeButtonClick(object sender, RoutedEventArgs e)
    {
        ColorPopup.IsOpen = false;
        SizePopup.IsOpen = !SizePopup.IsOpen;
    }

    private void OnColorClick(object sender, RoutedEventArgs e)
    {
        if (sender is Button b && b.Tag is string hex)
        {
            _strokeColor = (Color)ColorConverter.ConvertFromString(hex);
            _strokeBrush = new SolidColorBrush(_strokeColor);
            UpdateColorSwatch();
        }
        ColorPopup.IsOpen = false;
    }

    private void OnThicknessClick(object sender, RoutedEventArgs e)
    {
        if (sender is Button b && b.Tag is string val && double.TryParse(val, out double t))
        {
            _strokeThickness = t;
            UpdateSizeSwatch();
        }
        SizePopup.IsOpen = false;
    }

    private void OnUndoClick(object sender, RoutedEventArgs e) => Undo();
    private void OnCopyClick(object sender, RoutedEventArgs e) => CopyToClipboard();
    private void OnSaveClick(object sender, RoutedEventArgs e) => Save();
    private void OnPrintClick(object sender, RoutedEventArgs e) => Print();
    private void OnCloseClick(object sender, RoutedEventArgs e) => Close();
    private void OnPinClick(object sender, RoutedEventArgs e) => PinSelection();
    private void OnGoogleSearchClick(object sender, RoutedEventArgs e) =>
        ReverseSearchSelection(ReverseImageSearch.Engine.Google);
    private void OnBingSearchClick(object sender, RoutedEventArgs e) =>
        ReverseSearchSelection(ReverseImageSearch.Engine.Bing);

    private void Undo()
    {
        if (_history.Count == 0) return;
        var el = _history.Pop();
        AnnotationCanvas.Children.Remove(el);
    }

    // ---------- Text tool ----------

    private void BeginText(Point p)
    {
        var tb = new TextBox
        {
            Background = Brushes.Transparent,
            Foreground = _strokeBrush,
            BorderThickness = new Thickness(0),
            FontSize = 14 + _strokeThickness * 2,
            FontFamily = new FontFamily("Segoe UI"),
            MinWidth = 60,
            Padding = new Thickness(2),
            CaretBrush = _strokeBrush,
            AcceptsReturn = true
        };
        Canvas.SetLeft(tb, p.X);
        Canvas.SetTop(tb, p.Y);
        AnnotationCanvas.Children.Add(tb);
        _history.Push(tb);
        AnnotationCanvas.IsHitTestVisible = true;
        tb.Focus();
        tb.LostFocus += (_, _) =>
        {
            AnnotationCanvas.IsHitTestVisible = false;
            if (string.IsNullOrEmpty(tb.Text))
            {
                AnnotationCanvas.Children.Remove(tb);
                if (_history.Count > 0 && ReferenceEquals(_history.Peek(), tb)) _history.Pop();
            }
        };
        tb.PreviewKeyDown += (_, ke) =>
        {
            if (ke.Key == Key.Escape)
            {
                Keyboard.ClearFocus();
                ke.Handled = true;
            }
        };
    }

    // ---------- Arrow geometry ----------

    private static Geometry BuildArrowGeometry(Point start, Point end, double thickness)
    {
        var dx = end.X - start.X;
        var dy = end.Y - start.Y;
        var len = Math.Sqrt(dx * dx + dy * dy);
        if (len < 1) return Geometry.Empty;

        double headLen = Math.Max(12, thickness * 4);
        double headWidth = Math.Max(8, thickness * 3);

        // Point where shaft ends / head begins
        double ux = dx / len, uy = dy / len;
        Point shaftEnd = new(end.X - ux * headLen, end.Y - uy * headLen);

        // Perpendicular
        double px = -uy, py = ux;
        Point left = new(shaftEnd.X + px * headWidth, shaftEnd.Y + py * headWidth);
        Point right = new(shaftEnd.X - px * headWidth, shaftEnd.Y - py * headWidth);

        var group = new GeometryGroup { FillRule = FillRule.Nonzero };

        var shaft = new LineGeometry(start, shaftEnd);
        group.Children.Add(shaft);

        var head = new PathGeometry();
        var fig = new PathFigure { StartPoint = end, IsClosed = true, IsFilled = true };
        fig.Segments.Add(new LineSegment(left, true));
        fig.Segments.Add(new LineSegment(right, true));
        head.Figures.Add(fig);
        group.Children.Add(head);

        return group;
    }

    // ---------- Blur tool ----------

    private void CommitBlur(Rectangle marker)
    {
        double left = Canvas.GetLeft(marker);
        double top = Canvas.GetTop(marker);
        double w = marker.Width;
        double h = marker.Height;
        if (w < 2 || h < 2) return;

        // Full-size pre-blurred image, clipped to the drawn rectangle.
        var img = new Image
        {
            Source = _blurredSource,
            Width = RootGrid.ActualWidth,
            Height = RootGrid.ActualHeight,
            Stretch = Stretch.Fill,
            Clip = new RectangleGeometry(new Rect(left, top, w, h))
        };
        Canvas.SetLeft(img, 0);
        Canvas.SetTop(img, 0);
        AnnotationCanvas.Children.Add(img);
        _history.Push(img);
    }

    // ---------- Export ----------

    private void PrepareForExport()
    {
        ColorPopup.IsOpen = false;
        SizePopup.IsOpen = false;
        VToolbar.Visibility = Visibility.Collapsed;
        HToolbar.Visibility = Visibility.Collapsed;
        SelectionBorder.Visibility = Visibility.Collapsed;
        SizeLabel.Visibility = Visibility.Collapsed;
        HandleCanvas.Visibility = Visibility.Collapsed;
        DimCanvas.Visibility = Visibility.Collapsed;
        CursorHint.Visibility = Visibility.Collapsed;
        PreviewCanvas.Visibility = Visibility.Collapsed;
        Keyboard.ClearFocus();
        UpdateLayout();
    }

    private void RestoreAfterExport()
    {
        VToolbar.Visibility = Visibility.Visible;
        HToolbar.Visibility = Visibility.Visible;
        SelectionBorder.Visibility = Visibility.Visible;
        SizeLabel.Visibility = Visibility.Visible;
        HandleCanvas.Visibility = Visibility.Visible;
        DimCanvas.Visibility = Visibility.Visible;
        PreviewCanvas.Visibility = Visibility.Visible;
    }

    private BitmapSource? RenderSelection()
    {
        if (_selection.Width < 1 || _selection.Height < 1) return null;

        var dpi = VisualTreeHelper.GetDpi(this);
        double scale = dpi.DpiScaleX; // assume square pixels

        PrepareForExport();
        try
        {
            int pxW = (int)Math.Round(RootGrid.ActualWidth * scale);
            int pxH = (int)Math.Round(RootGrid.ActualHeight * scale);
            var rtb = new RenderTargetBitmap(pxW, pxH, 96 * scale, 96 * scale, PixelFormats.Pbgra32);
            rtb.Render(RootGrid);

            int sx = (int)Math.Round(_selection.X * scale);
            int sy = (int)Math.Round(_selection.Y * scale);
            int sw = (int)Math.Round(_selection.Width * scale);
            int sh = (int)Math.Round(_selection.Height * scale);
            sx = Math.Clamp(sx, 0, pxW - 1);
            sy = Math.Clamp(sy, 0, pxH - 1);
            sw = Math.Clamp(sw, 1, pxW - sx);
            sh = Math.Clamp(sh, 1, pxH - sy);

            var cropped = new CroppedBitmap(rtb, new Int32Rect(sx, sy, sw, sh));
            cropped.Freeze();
            return cropped;
        }
        finally
        {
            RestoreAfterExport();
        }
    }

    private void CopyToClipboard()
    {
        var img = RenderSelection();
        if (img == null) return;
        try
        {
            Clipboard.SetImage(img);
        }
        catch
        {
            // Clipboard can occasionally be locked; swallow to keep UX smooth.
        }
        Close();
    }

    private void Print()
    {
        var img = RenderSelection();
        if (img == null) return;

        var dlg = new System.Windows.Controls.PrintDialog();
        if (dlg.ShowDialog() != true) return;

        var visual = new DrawingVisual();
        using (var dc = visual.RenderOpen())
        {
            // Fit inside the printable area, preserving aspect ratio.
            double pageW = dlg.PrintableAreaWidth;
            double pageH = dlg.PrintableAreaHeight;
            double scale = Math.Min(pageW / img.Width, pageH / img.Height);
            double w = img.Width * scale;
            double h = img.Height * scale;
            double x = (pageW - w) / 2;
            double y = (pageH - h) / 2;
            dc.DrawImage(img, new Rect(x, y, w, h));
        }

        try
        {
            dlg.PrintVisual(visual, "Snapboard capture");
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Snapboard — print failed",
                            MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        Close();
    }

    /// <summary>
    /// Pin the current selection to the screen as a floating always-on-top
    /// thumbnail window (Snipaste-style). This is a feature ShareX does not
    /// natively provide, and is a major reason power users pick Snapboard.
    /// </summary>
    private void PinSelection()
    {
        var img = RenderSelection();
        if (img == null) return;

        // Remember where the selection sat in DIPs relative to this window so
        // the pin appears exactly "stuck" to the original spot.
        var origin = PointToScreen(new Point(_selection.X, _selection.Y));

        var pin = new PinWindow(img, origin);
        pin.Show();
        Close();
    }

    /// <summary>
    /// Send the current selection to Google Images or Bing Visual Search.
    /// The upload runs asynchronously so the UI never blocks, and a tray
    /// balloon surfaces the result (success/failure) to keep the flow
    /// friction-free.
    /// </summary>
    private void ReverseSearchSelection(ReverseImageSearch.Engine engine)
    {
        var img = RenderSelection();
        if (img == null) return;

        // Freeze so we can safely hand the bitmap to a background thread.
        if (img.CanFreeze && !img.IsFrozen) img.Freeze();

        // Close the overlay immediately; users expect the screenshot UI to
        // disappear while the browser opens with their results.
        Close();

        _ = Task.Run(async () =>
        {
            try
            {
                await ReverseImageSearch.SearchAsync(engine, img);
            }
            catch
            {
                // ReverseImageSearch already handles its own errors and falls
                // back to clipboard + landing page.
            }
        });
    }

    private void Save()
    {
        var img = RenderSelection();
        if (img == null) return;

        var settings = ((App)Application.Current).Settings;
        bool isJpeg = settings.DefaultFormat.Equals("jpg", StringComparison.OrdinalIgnoreCase)
                   || settings.DefaultFormat.Equals("jpeg", StringComparison.OrdinalIgnoreCase);
        string ext = isJpeg ? ".jpg" : ".png";
        string fileName = BitmapSaver.BuildDefaultFileName(ext);
        string path;

        if (settings.AutoSaveAfterCapture)
        {
            path = System.IO.Path.Combine(SettingsService.ResolveSaveDirectory(settings), fileName);
        }
        else
        {
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
            path = dlg.FileName;
        }

        try
        {
            BitmapSaver.Save(img, path, settings.JpegQuality);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Snapboard — save failed", MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        Close();
    }

    // ---------- Cursor hint ----------

    private void PositionCursorHint(Point cursor)
    {
        if (CursorHint.Visibility != Visibility.Visible) CursorHint.Visibility = Visibility.Visible;
        CursorHint.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        double w = CursorHint.DesiredSize.Width;
        double h = CursorHint.DesiredSize.Height;

        double offsetX = 18, offsetY = 22;
        double left = cursor.X + offsetX;
        double top  = cursor.Y + offsetY;

        if (left + w > ActualWidth  - 6) left = cursor.X - w - offsetX;
        if (top  + h > ActualHeight - 6) top  = cursor.Y - h - offsetY;
        if (left < 6) left = 6;
        if (top  < 6) top  = 6;

        CursorHint.Margin = new Thickness(left, top, 0, 0);
    }
}
