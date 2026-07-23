using System.Windows;
using System.Windows.Media;
using Color = System.Windows.Media.Color;
using Point = System.Windows.Point;

namespace ScreenCaptureApp.App;

internal sealed record AnnotationVisual(
    EditorTool Tool,
    Point Start,
    Point End,
    Color Color,
    double Width,
    IReadOnlyList<Point>? Points = null,
    string? Text = null);
