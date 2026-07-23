using ScreenCaptureApp.Core.Geometry;

namespace ScreenCaptureApp.Core.Editor;

public enum SelectionHandle
{
    None,
    TopLeft,
    Top,
    TopRight,
    Right,
    BottomRight,
    Bottom,
    BottomLeft,
    Left,
}

public enum NudgeDirection
{
    Left,
    Up,
    Right,
    Down,
}

/// <summary>An immutable, non-empty region selection in physical desktop pixels.</summary>
public sealed record SelectionState
{
    public SelectionState(PhysicalRect bounds)
    {
        if (bounds.IsEmpty)
        {
            throw new ArgumentException("A selection must have positive dimensions.", nameof(bounds));
        }

        Bounds = bounds;
    }

    public PhysicalRect Bounds { get; }

    public int Width => Bounds.Width;

    public int Height => Bounds.Height;

    public static SelectionState CreateFromDrag(PhysicalPoint start, PhysicalPoint end)
    {
        var bounds = PhysicalRect.FromPoints(start, end);
        return new SelectionState(bounds);
    }

    public static bool TryCreateFromDrag(PhysicalPoint start, PhysicalPoint end, out SelectionState? selection)
    {
        var bounds = PhysicalRect.FromPoints(start, end);
        selection = bounds.IsEmpty ? null : new SelectionState(bounds);
        return selection is not null;
    }

    public SelectionState MoveBy(PhysicalVector delta, PhysicalRect captureBounds) =>
        new(Bounds.Translate(delta).ClampInside(captureBounds));

    public SelectionState ClampTo(PhysicalRect captureBounds) =>
        new(Bounds.ClampInside(captureBounds));

    public SelectionState Nudge(NudgeDirection direction, int amount, PhysicalRect captureBounds)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(amount);
        var delta = direction switch
        {
            NudgeDirection.Left => new PhysicalVector(-amount, 0),
            NudgeDirection.Up => new PhysicalVector(0, -amount),
            NudgeDirection.Right => new PhysicalVector(amount, 0),
            NudgeDirection.Down => new PhysicalVector(0, amount),
            _ => throw new ArgumentOutOfRangeException(nameof(direction)),
        };

        return MoveBy(delta, captureBounds);
    }

    public SelectionState Resize(
        SelectionHandle handle,
        PhysicalVector delta,
        PhysicalRect captureBounds,
        PhysicalSize? minimumSize = null)
    {
        if (handle == SelectionHandle.None)
        {
            return this;
        }

        var minimum = minimumSize ?? new PhysicalSize(1, 1);
        if (minimum.IsEmpty)
        {
            throw new ArgumentException("Minimum selection size must be positive.", nameof(minimumSize));
        }

        if (minimum.Width > captureBounds.Width || minimum.Height > captureBounds.Height)
        {
            throw new ArgumentException("Minimum selection size cannot exceed capture bounds.", nameof(minimumSize));
        }

        var current = Bounds.ClampInside(captureBounds);
        var left = current.Left;
        var top = current.Top;
        var right = current.Right;
        var bottom = current.Bottom;

        if (MovesLeft(handle))
        {
            left = Math.Clamp(checked(left + delta.X), captureBounds.Left, right - minimum.Width);
        }

        if (MovesRight(handle))
        {
            right = Math.Clamp(checked(right + delta.X), left + minimum.Width, captureBounds.Right);
        }

        if (MovesTop(handle))
        {
            top = Math.Clamp(checked(top + delta.Y), captureBounds.Top, bottom - minimum.Height);
        }

        if (MovesBottom(handle))
        {
            bottom = Math.Clamp(checked(bottom + delta.Y), top + minimum.Height, captureBounds.Bottom);
        }

        return new SelectionState(PhysicalRect.FromEdges(left, top, right, bottom));
    }

    public SelectionHandle HitTestHandle(PhysicalPoint point, int tolerance)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(tolerance);

        var nearLeft = Math.Abs((long)point.X - Bounds.Left) <= tolerance;
        var nearRight = Math.Abs((long)point.X - Bounds.Right) <= tolerance;
        var nearTop = Math.Abs((long)point.Y - Bounds.Top) <= tolerance;
        var nearBottom = Math.Abs((long)point.Y - Bounds.Bottom) <= tolerance;
        var withinHorizontal = point.X >= (long)Bounds.Left - tolerance && point.X <= (long)Bounds.Right + tolerance;
        var withinVertical = point.Y >= (long)Bounds.Top - tolerance && point.Y <= (long)Bounds.Bottom + tolerance;

        if (nearLeft && nearTop)
        {
            return SelectionHandle.TopLeft;
        }

        if (nearRight && nearTop)
        {
            return SelectionHandle.TopRight;
        }

        if (nearRight && nearBottom)
        {
            return SelectionHandle.BottomRight;
        }

        if (nearLeft && nearBottom)
        {
            return SelectionHandle.BottomLeft;
        }

        if (nearTop && withinHorizontal)
        {
            return SelectionHandle.Top;
        }

        if (nearRight && withinVertical)
        {
            return SelectionHandle.Right;
        }

        if (nearBottom && withinHorizontal)
        {
            return SelectionHandle.Bottom;
        }

        return nearLeft && withinVertical ? SelectionHandle.Left : SelectionHandle.None;
    }

    private static bool MovesLeft(SelectionHandle handle) =>
        handle is SelectionHandle.Left or SelectionHandle.TopLeft or SelectionHandle.BottomLeft;

    private static bool MovesRight(SelectionHandle handle) =>
        handle is SelectionHandle.Right or SelectionHandle.TopRight or SelectionHandle.BottomRight;

    private static bool MovesTop(SelectionHandle handle) =>
        handle is SelectionHandle.Top or SelectionHandle.TopLeft or SelectionHandle.TopRight;

    private static bool MovesBottom(SelectionHandle handle) =>
        handle is SelectionHandle.Bottom or SelectionHandle.BottomLeft or SelectionHandle.BottomRight;
}
