namespace ScreenCaptureApp.Core.Geometry;

/// <summary>
/// A half-open rectangle in signed physical desktop pixels. The right and bottom
/// edges are excluded, so width and height are also exact output dimensions.
/// </summary>
public readonly record struct PhysicalRect
{
    public PhysicalRect(int x, int y, int width, int height)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(width);
        ArgumentOutOfRangeException.ThrowIfNegative(height);

        _ = checked(x + width);
        _ = checked(y + height);

        X = x;
        Y = y;
        Width = width;
        Height = height;
    }

    public int X { get; }

    public int Y { get; }

    public int Width { get; }

    public int Height { get; }

    public int Left => X;

    public int Top => Y;

    public int Right => X + Width;

    public int Bottom => Y + Height;

    public PhysicalPoint Location => new(X, Y);

    public PhysicalSize Size => new(Width, Height);

    public bool IsEmpty => Width == 0 || Height == 0;

    public static PhysicalRect FromPoints(PhysicalPoint first, PhysicalPoint second)
    {
        var left = Math.Min(first.X, second.X);
        var top = Math.Min(first.Y, second.Y);
        var width = checked(Math.Max(first.X, second.X) - left);
        var height = checked(Math.Max(first.Y, second.Y) - top);
        return new PhysicalRect(left, top, width, height);
    }

    public bool Contains(PhysicalPoint point) =>
        point.X >= Left && point.X < Right && point.Y >= Top && point.Y < Bottom;

    public bool Contains(PhysicalRect rectangle) =>
        rectangle.Left >= Left && rectangle.Top >= Top &&
        rectangle.Right <= Right && rectangle.Bottom <= Bottom;

    public bool IntersectsWith(PhysicalRect rectangle) =>
        rectangle.Left < Right && rectangle.Right > Left &&
        rectangle.Top < Bottom && rectangle.Bottom > Top;

    public PhysicalRect Intersect(PhysicalRect rectangle)
    {
        var left = Math.Max(Left, rectangle.Left);
        var top = Math.Max(Top, rectangle.Top);
        var right = Math.Min(Right, rectangle.Right);
        var bottom = Math.Min(Bottom, rectangle.Bottom);

        return right <= left || bottom <= top
            ? new PhysicalRect(left, top, 0, 0)
            : FromEdges(left, top, right, bottom);
    }

    public PhysicalRect Union(PhysicalRect rectangle)
    {
        if (IsEmpty)
        {
            return rectangle;
        }

        if (rectangle.IsEmpty)
        {
            return this;
        }

        return FromEdges(
            Math.Min(Left, rectangle.Left),
            Math.Min(Top, rectangle.Top),
            Math.Max(Right, rectangle.Right),
            Math.Max(Bottom, rectangle.Bottom));
    }

    public PhysicalRect Translate(PhysicalVector delta) =>
        new(checked(X + delta.X), checked(Y + delta.Y), Width, Height);

    public PhysicalRect ClampInside(PhysicalRect bounds)
    {
        if (bounds.IsEmpty)
        {
            throw new ArgumentException("Clamp bounds must have positive dimensions.", nameof(bounds));
        }

        var width = Math.Min(Width, bounds.Width);
        var height = Math.Min(Height, bounds.Height);
        var x = Math.Clamp(X, bounds.Left, bounds.Right - width);
        var y = Math.Clamp(Y, bounds.Top, bounds.Bottom - height);
        return new PhysicalRect(x, y, width, height);
    }

    public static PhysicalRect FromEdges(int left, int top, int right, int bottom)
    {
        if (right < left)
        {
            throw new ArgumentOutOfRangeException(nameof(right), "Right edge cannot precede left edge.");
        }

        if (bottom < top)
        {
            throw new ArgumentOutOfRangeException(nameof(bottom), "Bottom edge cannot precede top edge.");
        }

        return new PhysicalRect(left, top, checked(right - left), checked(bottom - top));
    }
}
