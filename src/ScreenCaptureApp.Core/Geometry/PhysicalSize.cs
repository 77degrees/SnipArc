namespace ScreenCaptureApp.Core.Geometry;

/// <summary>A non-negative size measured in physical pixels.</summary>
public readonly record struct PhysicalSize
{
    public PhysicalSize(int width, int height)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(width);
        ArgumentOutOfRangeException.ThrowIfNegative(height);

        Width = width;
        Height = height;
    }

    public int Width { get; }

    public int Height { get; }

    public bool IsEmpty => Width == 0 || Height == 0;
}
