using System.Collections.Immutable;
using ScreenCaptureApp.Core.Geometry;

namespace ScreenCaptureApp.Core.Displays;

/// <summary>An immutable snapshot of the displays participating in a capture.</summary>
public sealed class DisplayTopology
{
    public DisplayTopology(IEnumerable<DisplayInfo> displays)
    {
        ArgumentNullException.ThrowIfNull(displays);

        Displays = ImmutableArray.CreateRange(displays);
        if (Displays.IsEmpty)
        {
            throw new ArgumentException("At least one display is required.", nameof(displays));
        }

        if (Displays.Select(static display => display.Id).Distinct(StringComparer.OrdinalIgnoreCase).Count() != Displays.Length)
        {
            throw new ArgumentException("Display identifiers must be unique.", nameof(displays));
        }

        if (Displays.Count(static display => display.IsPrimary) != 1)
        {
            throw new ArgumentException("Exactly one display must be primary.", nameof(displays));
        }

        VirtualBounds = Displays.Skip(1).Aggregate(
            Displays[0].Bounds,
            static (bounds, display) => bounds.Union(display.Bounds));
    }

    public ImmutableArray<DisplayInfo> Displays { get; }

    public PhysicalRect VirtualBounds { get; }

    public DisplayInfo PrimaryDisplay => Displays.Single(static display => display.IsPrimary);

    public DisplayInfo? FindContaining(PhysicalPoint point) =>
        Displays.FirstOrDefault(display => display.Bounds.Contains(point));

    public DisplayInfo GetById(string id)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        return Displays.FirstOrDefault(display => string.Equals(display.Id, id, StringComparison.OrdinalIgnoreCase))
            ?? throw new KeyNotFoundException($"Display '{id}' does not exist in this topology.");
    }

    public ImmutableArray<PhysicalRect> ClipToDisplays(PhysicalRect rectangle) =>
        Displays
            .Select(display => display.Bounds.Intersect(rectangle))
            .Where(static clipped => !clipped.IsEmpty)
            .ToImmutableArray();
}

/// <summary>Supplies one immutable display snapshot at capture activation.</summary>
public interface IDisplayTopologyProvider
{
    public DisplayTopology GetCurrent();
}
