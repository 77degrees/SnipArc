using ScreenCaptureApp.Core.Displays;
using ScreenCaptureApp.Core.Geometry;

namespace ScreenCaptureApp.Core.Tests.Displays;

public sealed class DisplayTopologyTests
{
    [Fact]
    public void Constructor_ComputesVirtualBoundsAcrossNegativeOriginsAndGaps()
    {
        var topology = new DisplayTopology(
        [
            Display("primary", new PhysicalRect(0, 0, 1920, 1080), isPrimary: true),
            Display("left", new PhysicalRect(-2560, 200, 2560, 1440)),
            Display("above", new PhysicalRect(400, -1200, 1920, 1200)),
        ]);

        Assert.Equal(new PhysicalRect(-2560, -1200, 4880, 2840), topology.VirtualBounds);
        Assert.Equal("primary", topology.PrimaryDisplay.Id);
    }

    [Fact]
    public void ClipToDisplays_OmitsPhysicalGapAndPreservesCrossMonitorPieces()
    {
        var topology = new DisplayTopology(
        [
            Display("primary", new PhysicalRect(0, 0, 100, 100), isPrimary: true),
            Display("right", new PhysicalRect(120, 0, 100, 100)),
        ]);

        var pieces = topology.ClipToDisplays(new PhysicalRect(80, 10, 60, 20));

        Assert.True(pieces.SequenceEqual(
        [
            new PhysicalRect(80, 10, 20, 20),
            new PhysicalRect(120, 10, 20, 20),
        ]));
    }

    [Fact]
    public void Constructor_CopiesInputCollection()
    {
        var source = new List<DisplayInfo>
        {
            Display("primary", new PhysicalRect(0, 0, 100, 100), isPrimary: true),
        };
        var topology = new DisplayTopology(source);

        source.Clear();

        Assert.Single(topology.Displays);
    }

    [Fact]
    public void Constructor_RequiresUniqueIdentifiersAndOnePrimary()
    {
        Assert.Throws<ArgumentException>(() => new DisplayTopology(
        [
            Display("same", new PhysicalRect(0, 0, 100, 100), isPrimary: true),
            Display("SAME", new PhysicalRect(100, 0, 100, 100)),
        ]));

        Assert.Throws<ArgumentException>(() => new DisplayTopology(
        [
            Display("one", new PhysicalRect(0, 0, 100, 100)),
        ]));
    }

    private static DisplayInfo Display(string id, PhysicalRect bounds, bool isPrimary = false) =>
        new(id, bounds, bounds, 96, 96, isPrimary);
}
