using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Brushes = System.Windows.Media.Brushes;
using Color = System.Windows.Media.Color;
using Cursors = System.Windows.Input.Cursors;
using MouseEventArgs = System.Windows.Input.MouseEventArgs;
using Pen = System.Windows.Media.Pen;
using Point = System.Windows.Point;
using WpfFlowDirection = System.Windows.FlowDirection;

namespace ScreenCaptureApp.App;

internal sealed class CaptureSurface : FrameworkElement
{
    private const double HandleRadius = 4;
    private readonly List<AnnotationVisual> _annotations = [];
    private readonly Stack<AnnotationVisual> _redo = [];
    private readonly IReadOnlyList<WindowSuggestion> _windowSuggestions;
    private Point _dragStart;
    private Point _lastPoint;
    private List<Point>? _freehand;
    private DragMode _dragMode;
    private Rect _initialSelection;
    private AnnotationVisual? _preview;
    private WindowSuggestion? _hoveredWindow;
    private WindowSuggestion? _pressedWindow;
    private bool _manualDragStarted;

    public CaptureSurface(BitmapSource source, IReadOnlyList<WindowSuggestion>? windowSuggestions = null)
    {
        Source = source;
        _windowSuggestions = windowSuggestions ?? [];
        Focusable = true;
        Cursor = Cursors.Cross;
        SnapsToDevicePixels = true;
        RenderOptions.SetBitmapScalingMode(this, BitmapScalingMode.HighQuality);
    }

    public BitmapSource Source { get; }
    public Rect Selection { get; private set; } = Rect.Empty;
    public EditorTool Tool { get; set; } = EditorTool.Select;
    public Color AnnotationColor { get; set; } = Colors.Red;
    public double StrokeWidth { get; set; } = 3;
    public bool HasSelection => !Selection.IsEmpty && Selection.Width >= 1 && Selection.Height >= 1;
    public bool HasOpaqueRedactions => _annotations.Any(static annotation => annotation.Tool == EditorTool.Redact);
    public bool IsSelectionLocked => _annotations.Count > 0;
    public bool CanUndo => _annotations.Count > 0;
    public bool CanRedo => _redo.Count > 0;
    public event EventHandler? StateChanged;
    public event EventHandler<Point>? TextRequested;

    public void Undo()
    {
        if (_annotations.Count == 0) return;
        _redo.Push(_annotations[^1]);
        _annotations.RemoveAt(_annotations.Count - 1);
        InvalidateVisual();
        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    public void Redo()
    {
        if (_redo.Count == 0) return;
        _annotations.Add(_redo.Pop());
        InvalidateVisual();
        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    public void AddText(Point point, string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return;
        Commit(new AnnotationVisual(EditorTool.Text, point, point, AnnotationColor, StrokeWidth, Text: text.Trim()));
    }

    public void SelectAll()
    {
        if (IsSelectionLocked) return;
        _hoveredWindow = null;
        Selection = new Rect(0, 0, Source.PixelWidth, Source.PixelHeight);
        InvalidateVisual();
        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    public void NudgeSelection(double dx, double dy)
    {
        if (!HasSelection || IsSelectionLocked) return;
        var candidate = new Rect(Selection.X + dx, Selection.Y + dy, Selection.Width, Selection.Height);
        candidate.X = Math.Clamp(candidate.X, 0, Source.PixelWidth - candidate.Width);
        candidate.Y = Math.Clamp(candidate.Y, 0, Source.PixelHeight - candidate.Height);
        Selection = candidate;
        InvalidateVisual();
        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    public BitmapSource RenderSelection()
    {
        if (!HasSelection) throw new InvalidOperationException("Select an area first.");
        var pixels = NormalizeSelection();
        var cropped = new CroppedBitmap(Source, pixels);
        var visual = new DrawingVisual();
        using (var dc = visual.RenderOpen())
        {
            dc.DrawImage(cropped, new Rect(0, 0, pixels.Width, pixels.Height));
            foreach (var annotation in _annotations.Where(static annotation => annotation.Tool != EditorTool.Redact))
            {
                DrawAnnotation(dc, annotation, new Vector(-pixels.X, -pixels.Y), 1, forExport: true);
            }
        }

        var rendered = new RenderTargetBitmap(pixels.Width, pixels.Height, 96, 96, PixelFormats.Pbgra32);
        rendered.Render(visual);
        rendered.Freeze();
        return ApplyOpaqueRedactions(rendered, pixels);
    }

    public void RefreshWindowSuggestion(Point viewPoint)
    {
        if (HasSelection || Tool != EditorTool.Select) return;
        var point = Clamp(ToPixel(viewPoint));
        UpdateWindowSuggestion(point);
        UpdateSelectionCursor(point);
    }

    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        Focus();
        CaptureMouse();
        var point = ToPixel(e.GetPosition(this));
        _dragStart = _lastPoint = point;
        _initialSelection = Selection;
        if (Tool == EditorTool.Select)
        {
            if (IsSelectionLocked)
            {
                _dragMode = DragMode.None;
                ReleaseMouseCapture();
                return;
            }

            var handle = HitHandle(point);
            _dragMode = handle == Handle.None ? (Selection.Contains(point) ? DragMode.Move : DragMode.Create) : (DragMode)handle;
            if (_dragMode == DragMode.Create)
            {
                _pressedWindow = !HasSelection ? WindowSuggestionSelector.FindAt(_windowSuggestions, point) : null;
                _manualDragStarted = false;
                Selection = new Rect(point, point);
            }
        }
        else if (!HasSelection || !Selection.Contains(point))
        {
            _dragMode = DragMode.None;
        }
        else if (Tool == EditorTool.Text)
        {
            ReleaseMouseCapture();
            TextRequested?.Invoke(this, point);
        }
        else
        {
            _dragMode = DragMode.Annotate;
            if (Tool == EditorTool.Pen) _freehand = [point];
            _preview = CreateAnnotation(point, point);
        }
        InvalidateVisual();
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        var point = Clamp(ToPixel(e.GetPosition(this)));
        if (e.LeftButton != MouseButtonState.Pressed || _dragMode == DragMode.None)
        {
            if (!HasSelection && Tool == EditorTool.Select) UpdateWindowSuggestion(point);
            UpdateSelectionCursor(point);
            return;
        }

        if (_dragMode == DragMode.Create)
        {
            if (!_manualDragStarted)
            {
                var thresholdX = 4 * Source.PixelWidth / Math.Max(1, ActualWidth);
                var thresholdY = 4 * Source.PixelHeight / Math.Max(1, ActualHeight);
                if (Math.Abs(point.X - _dragStart.X) < thresholdX && Math.Abs(point.Y - _dragStart.Y) < thresholdY) return;

                _manualDragStarted = true;
                _hoveredWindow = null;
            }

            Selection = NormalizeRect(_dragStart, point);
        }
        else if (_dragMode == DragMode.Move)
        {
            var delta = point - _lastPoint;
            var moved = new Rect(Selection.X + delta.X, Selection.Y + delta.Y, Selection.Width, Selection.Height);
            moved.X = Math.Clamp(moved.X, 0, Source.PixelWidth - moved.Width);
            moved.Y = Math.Clamp(moved.Y, 0, Source.PixelHeight - moved.Height);
            Selection = moved;
            _lastPoint = point;
        }
        else if (_dragMode is >= DragMode.NorthWest and <= DragMode.West)
        {
            Selection = ResizeSelection(_dragMode, point);
        }
        else if (_dragMode == DragMode.Annotate)
        {
            point = ClampToSelection(point);
            if (_freehand is not null) _freehand.Add(point);
            _preview = CreateAnnotation(_dragStart, point);
        }
        InvalidateVisual();
        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    private void UpdateSelectionCursor(Point point)
    {
        if (Tool != EditorTool.Select)
        {
            Cursor = Cursors.Cross;
            return;
        }

        Cursor = HitHandle(point) switch
        {
            Handle.NorthWest or Handle.SouthEast => Cursors.SizeNWSE,
            Handle.NorthEast or Handle.SouthWest => Cursors.SizeNESW,
            Handle.North or Handle.South => Cursors.SizeNS,
            Handle.East or Handle.West => Cursors.SizeWE,
            _ when Selection.Contains(point) => Cursors.SizeAll,
            _ when _hoveredWindow is not null => Cursors.Hand,
            _ => Cursors.Cross
        };
    }

    private void UpdateWindowSuggestion(Point point)
    {
        var candidate = WindowSuggestionSelector.FindAt(_windowSuggestions, point);
        if (Equals(candidate, _hoveredWindow)) return;

        _hoveredWindow = candidate;
        InvalidateVisual();
    }

    protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
    {
        ReleaseMouseCapture();
        if (_dragMode == DragMode.Create && !_manualDragStarted && _pressedWindow is not null)
        {
            Selection = _pressedWindow.Bounds;
            _hoveredWindow = null;
        }

        if (_dragMode == DragMode.Annotate && _preview is not null) Commit(_preview);
        _preview = null;
        _freehand = null;
        _dragMode = DragMode.None;
        _pressedWindow = null;
        _manualDragStarted = false;
        if (Selection.Width < 1 || Selection.Height < 1) Selection = Rect.Empty;
        if (!HasSelection) UpdateWindowSuggestion(Clamp(ToPixel(e.GetPosition(this))));
        UpdateSelectionCursor(Clamp(ToPixel(e.GetPosition(this))));
        InvalidateVisual();
        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);
        if (ActualWidth <= 0 || ActualHeight <= 0) return;
        var sx = ActualWidth / Source.PixelWidth;
        var sy = ActualHeight / Source.PixelHeight;
        dc.DrawImage(Source, new Rect(0, 0, ActualWidth, ActualHeight));

        if (!HasSelection)
        {
            dc.DrawRectangle(new SolidColorBrush(Color.FromArgb(145, 0, 0, 0)), null, new Rect(0, 0, ActualWidth, ActualHeight));
            if (_hoveredWindow is not null)
            {
                DrawWindowSuggestion(dc, _hoveredWindow);
            }
            else
            {
                DrawHint(dc);
            }
            return;
        }

        var viewSelection = ToView(Selection);
        var shade = new SolidColorBrush(Color.FromArgb(145, 0, 0, 0));
        dc.DrawRectangle(shade, null, new Rect(0, 0, ActualWidth, viewSelection.Top));
        dc.DrawRectangle(shade, null, new Rect(0, viewSelection.Bottom, ActualWidth, Math.Max(0, ActualHeight - viewSelection.Bottom)));
        dc.DrawRectangle(shade, null, new Rect(0, viewSelection.Top, viewSelection.Left, viewSelection.Height));
        dc.DrawRectangle(shade, null, new Rect(viewSelection.Right, viewSelection.Top, Math.Max(0, ActualWidth - viewSelection.Right), viewSelection.Height));

        dc.PushClip(new RectangleGeometry(viewSelection));
        foreach (var annotation in _annotations) DrawAnnotation(dc, annotation, default, (sx + sy) / 2, forExport: false);
        if (_preview is not null) DrawAnnotation(dc, _preview, default, (sx + sy) / 2, forExport: false);
        dc.Pop();

        dc.DrawRectangle(null, new Pen(new SolidColorBrush(Color.FromArgb(115, 0, 0, 0)), 3), viewSelection);
        dc.DrawRectangle(null, new Pen(new SolidColorBrush(Color.FromRgb(91, 151, 255)), 1.25), viewSelection);
        DrawHandles(dc, viewSelection);
        DrawDimensions(dc, viewSelection);
    }

    private void DrawAnnotation(DrawingContext dc, AnnotationVisual item, Vector offset, double viewScale, bool forExport)
    {
        Point Map(Point p) => forExport ? p + offset : new Point(p.X * ActualWidth / Source.PixelWidth, p.Y * ActualHeight / Source.PixelHeight);
        var start = Map(item.Start);
        var end = Map(item.End);
        var color = item.Tool == EditorTool.Highlight ? Color.FromArgb(95, item.Color.R, item.Color.G, item.Color.B) : item.Color;
        var brush = new SolidColorBrush(color);
        var width = Math.Max(1, item.Width * viewScale);
        var pen = new Pen(brush, width) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round, LineJoin = PenLineJoin.Round };
        switch (item.Tool)
        {
            case EditorTool.Pen:
                if (item.Points is not { Count: > 1 }) break;
                var geometry = new StreamGeometry();
                using (var ctx = geometry.Open())
                {
                    ctx.BeginFigure(Map(item.Points[0]), false, false);
                    ctx.PolyLineTo(item.Points.Skip(1).Select(Map).ToList(), true, true);
                }
                dc.DrawGeometry(null, pen, geometry);
                break;
            case EditorTool.Line:
                dc.DrawLine(pen, start, end);
                break;
            case EditorTool.Arrow:
                dc.DrawLine(pen, start, end);
                var vector = start - end;
                if (vector.Length > 0)
                {
                    vector.Normalize();
                    var perpendicular = new Vector(-vector.Y, vector.X);
                    var length = Math.Max(10, width * 4);
                    var head = end + vector * length;
                    dc.DrawLine(pen, end, head + perpendicular * length * 0.45);
                    dc.DrawLine(pen, end, head - perpendicular * length * 0.45);
                }
                break;
            case EditorTool.Rectangle:
                dc.DrawRectangle(null, pen, NormalizeRect(start, end));
                break;
            case EditorTool.Highlight:
                dc.DrawRectangle(brush, null, NormalizeRect(start, end));
                break;
            case EditorTool.Redact:
                dc.DrawRectangle(Brushes.Black, null, NormalizeRect(start, end));
                break;
            case EditorTool.Pixelate:
                DrawPixelation(dc, item, offset, forExport);
                break;
            case EditorTool.Text:
                var typeface = new Typeface("Segoe UI");
                var text = new FormattedText(item.Text ?? string.Empty, System.Globalization.CultureInfo.CurrentUICulture,
                    WpfFlowDirection.LeftToRight, typeface, Math.Max(12, width * 5), brush, 1);
                dc.DrawText(text, start);
                break;
        }
    }

    private void DrawPixelation(DrawingContext dc, AnnotationVisual item, Vector offset, bool forExport)
    {
        var sourceRect = NormalizeRect(item.Start, item.End);
        var rect = new Int32Rect((int)sourceRect.X, (int)sourceRect.Y, Math.Max(1, (int)sourceRect.Width), Math.Max(1, (int)sourceRect.Height));
        var clippedX = Math.Clamp(rect.X, 0, Source.PixelWidth);
        var clippedY = Math.Clamp(rect.Y, 0, Source.PixelHeight);
        var clippedRight = Math.Clamp(rect.X + rect.Width, clippedX, Source.PixelWidth);
        var clippedBottom = Math.Clamp(rect.Y + rect.Height, clippedY, Source.PixelHeight);
        rect = new Int32Rect(clippedX, clippedY, clippedRight - clippedX, clippedBottom - clippedY);
        if (rect.Width <= 0 || rect.Height <= 0) return;
        var crop = new CroppedBitmap(Source, rect);
        var blockWidth = Math.Max(1, rect.Width / 12);
        var blockHeight = Math.Max(1, rect.Height / 12);
        var tiny = new TransformedBitmap(crop, new ScaleTransform((double)blockWidth / rect.Width, (double)blockHeight / rect.Height));
        RenderOptions.SetBitmapScalingMode(tiny, BitmapScalingMode.NearestNeighbor);
        var target = forExport
            ? new Rect(rect.X + offset.X, rect.Y + offset.Y, rect.Width, rect.Height)
            : ToView(new Rect(rect.X, rect.Y, rect.Width, rect.Height));
        dc.PushGuidelineSet(new GuidelineSet());
        dc.DrawImage(tiny, target);
        dc.Pop();
    }

    private BitmapSource ApplyOpaqueRedactions(BitmapSource rendered, Int32Rect cropBounds)
        => OpaqueRedactionCompositor.Apply(rendered, cropBounds, _annotations);

    private AnnotationVisual CreateAnnotation(Point start, Point end) =>
        new(Tool, start, end, AnnotationColor, StrokeWidth, _freehand?.ToArray());

    private void Commit(AnnotationVisual annotation)
    {
        _annotations.Add(annotation);
        _redo.Clear();
        InvalidateVisual();
        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    private Int32Rect NormalizeSelection()
    {
        var x = Math.Clamp((int)Math.Floor(Selection.X), 0, Source.PixelWidth - 1);
        var y = Math.Clamp((int)Math.Floor(Selection.Y), 0, Source.PixelHeight - 1);
        var right = Math.Clamp((int)Math.Ceiling(Selection.Right), x + 1, Source.PixelWidth);
        var bottom = Math.Clamp((int)Math.Ceiling(Selection.Bottom), y + 1, Source.PixelHeight);
        return new Int32Rect(x, y, right - x, bottom - y);
    }

    private Rect ResizeSelection(DragMode mode, Point point)
    {
        var left = mode is DragMode.NorthWest or DragMode.West or DragMode.SouthWest ? point.X : _initialSelection.Left;
        var right = mode is DragMode.NorthEast or DragMode.East or DragMode.SouthEast ? point.X : _initialSelection.Right;
        var top = mode is DragMode.NorthWest or DragMode.North or DragMode.NorthEast ? point.Y : _initialSelection.Top;
        var bottom = mode is DragMode.SouthWest or DragMode.South or DragMode.SouthEast ? point.Y : _initialSelection.Bottom;
        return NormalizeRect(new Point(left, top), new Point(right, bottom));
    }

    private Handle HitHandle(Point pixel)
    {
        if (!HasSelection) return Handle.None;
        var thresholdX = HandleRadius * Source.PixelWidth / Math.Max(1, ActualWidth) * 2;
        var thresholdY = HandleRadius * Source.PixelHeight / Math.Max(1, ActualHeight) * 2;
        var points = HandlePoints(Selection);
        for (var i = 0; i < points.Length; i++)
            if (Math.Abs(points[i].X - pixel.X) <= thresholdX && Math.Abs(points[i].Y - pixel.Y) <= thresholdY) return (Handle)(i + 2);
        return Handle.None;
    }

    private static void DrawHandles(DrawingContext dc, Rect selection)
    {
        var border = new Pen(new SolidColorBrush(Color.FromRgb(28, 31, 38)), 1);
        foreach (var point in HandlePoints(selection))
        {
            var handle = new Rect(point.X - 3.5, point.Y - 3.5, 7, 7);
            dc.DrawRoundedRectangle(Brushes.White, border, handle, 1.5, 1.5);
        }
    }

    private static Point[] HandlePoints(Rect r) =>
    [
        r.TopLeft, new(r.Left + r.Width / 2, r.Top), r.TopRight, new(r.Right, r.Top + r.Height / 2),
        r.BottomRight, new(r.Left + r.Width / 2, r.Bottom), r.BottomLeft, new(r.Left, r.Top + r.Height / 2)
    ];

    private void DrawHint(DrawingContext dc)
    {
        var text = new FormattedText("Hover a window and click  ·  Drag for an area  ·  Esc to cancel", System.Globalization.CultureInfo.CurrentUICulture,
            WpfFlowDirection.LeftToRight, new Typeface("Segoe UI Semibold"), 13, Brushes.White, VisualTreeHelper.GetDpi(this).PixelsPerDip);
        var origin = new Point((ActualWidth - text.Width) / 2, Math.Max(24, ActualHeight * .12));
        dc.DrawRoundedRectangle(new SolidColorBrush(Color.FromArgb(225, 29, 31, 35)), null,
            new Rect(origin.X - 10, origin.Y - 6, text.Width + 20, text.Height + 12), 8, 8);
        dc.DrawText(text, origin);
    }

    private void DrawWindowSuggestion(DrawingContext dc, WindowSuggestion suggestion)
    {
        var bounds = ToView(suggestion.Bounds);
        dc.PushClip(new RectangleGeometry(bounds));
        dc.DrawImage(Source, new Rect(0, 0, ActualWidth, ActualHeight));
        dc.Pop();

        dc.DrawRectangle(null, new Pen(new SolidColorBrush(Color.FromArgb(125, 0, 0, 0)), 3), bounds);
        dc.DrawRectangle(null, new Pen(new SolidColorBrush(Color.FromRgb(91, 151, 255)), 1.5), bounds);

        var title = suggestion.Title.Length > 48 ? $"{suggestion.Title[..45]}…" : suggestion.Title;
        var label = $"{title}  ·  Click to capture window";
        var text = new FormattedText(label, System.Globalization.CultureInfo.CurrentUICulture, WpfFlowDirection.LeftToRight,
            new Typeface("Segoe UI Semibold"), 11, Brushes.White, VisualTreeHelper.GetDpi(this).PixelsPerDip);
        var originX = Math.Clamp(bounds.Left + 6, 6, Math.Max(6, ActualWidth - text.Width - 12));
        var originY = bounds.Top >= text.Height + 15 ? bounds.Top - text.Height - 9 : bounds.Top + 7;
        var origin = new Point(originX, originY);
        dc.DrawRoundedRectangle(new SolidColorBrush(Color.FromArgb(242, 29, 31, 35)), null,
            new Rect(origin.X - 6, origin.Y - 3, text.Width + 12, text.Height + 6), 5, 5);
        dc.DrawText(text, origin);
    }

    private void DrawDimensions(DrawingContext dc, Rect viewSelection)
    {
        var label = $"{Math.Round(Selection.Width)} × {Math.Round(Selection.Height)} px";
        var text = new FormattedText(label, System.Globalization.CultureInfo.CurrentUICulture, WpfFlowDirection.LeftToRight,
            new Typeface("Segoe UI Semibold"), 11, Brushes.White, VisualTreeHelper.GetDpi(this).PixelsPerDip);
        var originY = viewSelection.Top >= text.Height + 15 ? viewSelection.Top - text.Height - 9 : viewSelection.Top + 7;
        var origin = new Point(viewSelection.Left + 6, originY);
        dc.DrawRoundedRectangle(new SolidColorBrush(Color.FromArgb(242, 29, 31, 35)), null,
            new Rect(origin.X - 6, origin.Y - 3, text.Width + 12, text.Height + 6), 5, 5);
        dc.DrawText(text, origin);
    }

    private Point ToPixel(Point p) => new(p.X * Source.PixelWidth / Math.Max(1, ActualWidth), p.Y * Source.PixelHeight / Math.Max(1, ActualHeight));
    private Rect ToView(Rect r) => new(r.X * ActualWidth / Source.PixelWidth, r.Y * ActualHeight / Source.PixelHeight,
        r.Width * ActualWidth / Source.PixelWidth, r.Height * ActualHeight / Source.PixelHeight);
    private Point Clamp(Point p) => new(Math.Clamp(p.X, 0, Source.PixelWidth), Math.Clamp(p.Y, 0, Source.PixelHeight));
    private Point ClampToSelection(Point p) => new(Math.Clamp(p.X, Selection.Left, Selection.Right), Math.Clamp(p.Y, Selection.Top, Selection.Bottom));
    private static Rect NormalizeRect(Point a, Point b) => new(Math.Min(a.X, b.X), Math.Min(a.Y, b.Y), Math.Abs(b.X - a.X), Math.Abs(b.Y - a.Y));

    private enum Handle { None = 0, NorthWest = 2, North, NorthEast, East, SouthEast, South, SouthWest, West }
    private enum DragMode { None = 0, Create, NorthWest, North, NorthEast, East, SouthEast, South, SouthWest, West, Move, Annotate }
}
