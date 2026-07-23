using System.Collections.Immutable;
using ScreenCaptureApp.Core.Geometry;

namespace ScreenCaptureApp.Core.Annotations;

/// <summary>A committed, immutable annotation in physical desktop coordinates.</summary>
public abstract record Annotation
{
    protected Annotation(Guid id)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("Annotation identifier cannot be empty.", nameof(id));
        }

        Id = id;
    }

    public Guid Id { get; }
}

public sealed record FreehandPathAnnotation : Annotation
{
    public FreehandPathAnnotation(
        Guid id,
        IEnumerable<PhysicalPoint> points,
        RgbaColor color,
        int strokeWidth) : base(id)
    {
        ArgumentNullException.ThrowIfNull(points);
        Points = ImmutableArray.CreateRange(points);
        if (Points.Length < 2)
        {
            throw new ArgumentException("A freehand path requires at least two points.", nameof(points));
        }

        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(strokeWidth);
        Color = color;
        StrokeWidth = strokeWidth;
    }

    public ImmutableArray<PhysicalPoint> Points { get; }

    public RgbaColor Color { get; }

    public int StrokeWidth { get; }
}

public sealed record LineAnnotation : Annotation
{
    public LineAnnotation(Guid id, PhysicalPoint start, PhysicalPoint end, RgbaColor color, int strokeWidth) : base(id)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(strokeWidth);
        if (start == end)
        {
            throw new ArgumentException("A line must have distinct endpoints.", nameof(end));
        }

        Start = start;
        End = end;
        Color = color;
        StrokeWidth = strokeWidth;
    }

    public PhysicalPoint Start { get; }

    public PhysicalPoint End { get; }

    public RgbaColor Color { get; }

    public int StrokeWidth { get; }
}

public sealed record ArrowAnnotation : Annotation
{
    public ArrowAnnotation(Guid id, PhysicalPoint start, PhysicalPoint end, RgbaColor color, int strokeWidth) : base(id)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(strokeWidth);
        if (start == end)
        {
            throw new ArgumentException("An arrow must have distinct endpoints.", nameof(end));
        }

        Start = start;
        End = end;
        Color = color;
        StrokeWidth = strokeWidth;
    }

    public PhysicalPoint Start { get; }

    public PhysicalPoint End { get; }

    public RgbaColor Color { get; }

    public int StrokeWidth { get; }
}

public sealed record RectangleAnnotation : Annotation
{
    public RectangleAnnotation(Guid id, PhysicalRect bounds, RgbaColor color, int strokeWidth) : base(id)
    {
        AnnotationGuards.RequireNonEmpty(bounds, nameof(bounds));
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(strokeWidth);
        Bounds = bounds;
        Color = color;
        StrokeWidth = strokeWidth;
    }

    public PhysicalRect Bounds { get; }

    public RgbaColor Color { get; }

    public int StrokeWidth { get; }
}

public sealed record HighlightAnnotation : Annotation
{
    public HighlightAnnotation(
        Guid id,
        IEnumerable<PhysicalPoint> points,
        RgbaColor color,
        int strokeWidth) : base(id)
    {
        ArgumentNullException.ThrowIfNull(points);
        Points = ImmutableArray.CreateRange(points);
        if (Points.Length < 2)
        {
            throw new ArgumentException("A highlight path requires at least two points.", nameof(points));
        }

        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(strokeWidth);
        Color = color;
        StrokeWidth = strokeWidth;
    }

    public ImmutableArray<PhysicalPoint> Points { get; }

    public RgbaColor Color { get; }

    public int StrokeWidth { get; }
}

public sealed record TextAnnotation : Annotation
{
    public TextAnnotation(
        Guid id,
        PhysicalPoint origin,
        string text,
        string fontFamily,
        int fontSize,
        RgbaColor color) : base(id)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(text);
        ArgumentException.ThrowIfNullOrWhiteSpace(fontFamily);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(fontSize);

        Origin = origin;
        Text = text;
        FontFamily = fontFamily;
        FontSize = fontSize;
        Color = color;
    }

    public PhysicalPoint Origin { get; }

    public string Text { get; }

    public string FontFamily { get; }

    public int FontSize { get; }

    public RgbaColor Color { get; }
}

public sealed record PixelateAnnotation : Annotation
{
    public PixelateAnnotation(Guid id, PhysicalRect bounds, int blockSize) : base(id)
    {
        AnnotationGuards.RequireNonEmpty(bounds, nameof(bounds));
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(blockSize);
        Bounds = bounds;
        BlockSize = blockSize;
    }

    public PhysicalRect Bounds { get; }

    public int BlockSize { get; }
}

public sealed record OpaqueRedactionAnnotation : Annotation
{
    public OpaqueRedactionAnnotation(Guid id, PhysicalRect bounds, RgbaColor color) : base(id)
    {
        AnnotationGuards.RequireNonEmpty(bounds, nameof(bounds));
        if (!color.IsOpaque)
        {
            throw new ArgumentException("A secure redaction color must be fully opaque.", nameof(color));
        }

        Bounds = bounds;
        Color = color;
    }

    public PhysicalRect Bounds { get; }

    public RgbaColor Color { get; }
}

internal static class AnnotationGuards
{
    public static void RequireNonEmpty(PhysicalRect bounds, string parameterName)
    {
        if (bounds.IsEmpty)
        {
            throw new ArgumentException("Annotation bounds must have positive dimensions.", parameterName);
        }
    }
}
