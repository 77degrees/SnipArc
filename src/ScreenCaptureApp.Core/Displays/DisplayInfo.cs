using ScreenCaptureApp.Core.Geometry;

namespace ScreenCaptureApp.Core.Displays;

/// <summary>Immutable physical-pixel information for one display.</summary>
public sealed record DisplayInfo
{
    public DisplayInfo(
        string id,
        PhysicalRect bounds,
        PhysicalRect workArea,
        uint dpiX,
        uint dpiY,
        bool isPrimary)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        if (bounds.IsEmpty)
        {
            throw new ArgumentException("Display bounds must have positive dimensions.", nameof(bounds));
        }

        if (workArea.IsEmpty || !bounds.Contains(workArea))
        {
            throw new ArgumentException("Work area must be non-empty and contained by display bounds.", nameof(workArea));
        }

        ArgumentOutOfRangeException.ThrowIfZero(dpiX);
        ArgumentOutOfRangeException.ThrowIfZero(dpiY);

        Id = id;
        Bounds = bounds;
        WorkArea = workArea;
        DpiX = dpiX;
        DpiY = dpiY;
        IsPrimary = isPrimary;
    }

    public string Id { get; }

    public PhysicalRect Bounds { get; }

    public PhysicalRect WorkArea { get; }

    public uint DpiX { get; }

    public uint DpiY { get; }

    public bool IsPrimary { get; }
}
