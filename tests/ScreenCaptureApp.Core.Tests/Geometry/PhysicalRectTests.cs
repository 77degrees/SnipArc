using ScreenCaptureApp.Core.Geometry;

namespace ScreenCaptureApp.Core.Tests.Geometry;

public sealed class PhysicalRectTests
{
    [Theory]
    [InlineData(10, 20, 40, 70)]
    [InlineData(40, 20, 10, 70)]
    [InlineData(10, 70, 40, 20)]
    [InlineData(40, 70, 10, 20)]
    public void FromPoints_NormalizesDragInEveryDirection(int startX, int startY, int endX, int endY)
    {
        var rectangle = PhysicalRect.FromPoints(
            new PhysicalPoint(startX, startY),
            new PhysicalPoint(endX, endY));

        Assert.Equal(new PhysicalRect(10, 20, 30, 50), rectangle);
    }

    [Fact]
    public void Constructor_SupportsNegativeDesktopCoordinates()
    {
        var rectangle = new PhysicalRect(-3840, -2160, 3840, 2160);

        Assert.Equal(0, rectangle.Right);
        Assert.Equal(0, rectangle.Bottom);
        Assert.True(rectangle.Contains(new PhysicalPoint(-1, -1)));
    }

    [Fact]
    public void ClampInside_ShrinksAndMovesRectangleIntoBounds()
    {
        var rectangle = new PhysicalRect(-200, 50, 500, 400);
        var bounds = new PhysicalRect(-100, -100, 300, 250);

        var result = rectangle.ClampInside(bounds);

        Assert.Equal(bounds, result);
    }

    [Fact]
    public void Intersect_ReturnsExactHalfOpenOverlap()
    {
        var first = new PhysicalRect(-100, 0, 200, 100);
        var second = new PhysicalRect(50, -50, 100, 100);

        Assert.Equal(new PhysicalRect(50, 0, 50, 50), first.Intersect(second));
    }

    [Fact]
    public void Constructor_RejectsDimensionsThatOverflowEdges()
    {
        Assert.Throws<OverflowException>(() => new PhysicalRect(int.MaxValue, 0, 1, 1));
    }
}
