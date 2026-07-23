using ScreenCaptureApp.Core.Editor;
using ScreenCaptureApp.Core.Geometry;

namespace ScreenCaptureApp.Core.Tests.Geometry;

public sealed class SelectionStateTests
{
    private static readonly PhysicalRect Desktop = new(-100, -100, 400, 300);

    [Fact]
    public void TryCreateFromDrag_RejectsZeroWidthSelection()
    {
        var created = SelectionState.TryCreateFromDrag(
            new PhysicalPoint(10, 20),
            new PhysicalPoint(10, 80),
            out var selection);

        Assert.False(created);
        Assert.Null(selection);
    }

    [Fact]
    public void MoveBy_ClampsAtNegativeDesktopEdgeAndPreservesSize()
    {
        var selection = new SelectionState(new PhysicalRect(-50, -50, 80, 60));

        var moved = selection.MoveBy(new PhysicalVector(-500, -500), Desktop);

        Assert.Equal(new PhysicalRect(-100, -100, 80, 60), moved.Bounds);
    }

    [Theory]
    [InlineData(SelectionHandle.Left, -20, 0, 80, 100, 70, 50)]
    [InlineData(SelectionHandle.TopLeft, -20, -30, 80, 70, 70, 80)]
    [InlineData(SelectionHandle.Top, 0, -30, 100, 70, 50, 80)]
    [InlineData(SelectionHandle.TopRight, 20, -30, 100, 70, 70, 80)]
    [InlineData(SelectionHandle.Right, 20, 0, 100, 100, 70, 50)]
    [InlineData(SelectionHandle.BottomRight, 20, 30, 100, 100, 70, 80)]
    [InlineData(SelectionHandle.Bottom, 0, 30, 100, 100, 50, 80)]
    [InlineData(SelectionHandle.BottomLeft, -20, 30, 80, 100, 70, 80)]
    public void Resize_MovesRequestedEdges(
        SelectionHandle handle,
        int deltaX,
        int deltaY,
        int expectedX,
        int expectedY,
        int expectedWidth,
        int expectedHeight)
    {
        var selection = new SelectionState(new PhysicalRect(100, 100, 50, 50));

        var resized = selection.Resize(handle, new PhysicalVector(deltaX, deltaY), Desktop);

        Assert.Equal(new PhysicalRect(expectedX, expectedY, expectedWidth, expectedHeight), resized.Bounds);
    }

    [Fact]
    public void Resize_DoesNotCrossOppositeEdgeOrLeaveDesktop()
    {
        var selection = new SelectionState(new PhysicalRect(-90, -90, 30, 30));

        var resized = selection.Resize(
            SelectionHandle.TopLeft,
            new PhysicalVector(1_000, 1_000),
            Desktop,
            new PhysicalSize(5, 7));

        Assert.Equal(new PhysicalRect(-65, -67, 5, 7), resized.Bounds);
        Assert.True(Desktop.Contains(resized.Bounds));
    }

    [Fact]
    public void Nudge_DefaultOnePixelBehaviorStopsAtBoundary()
    {
        var selection = new SelectionState(new PhysicalRect(-100, -100, 20, 20));

        var left = selection.Nudge(NudgeDirection.Left, 1, Desktop);
        var down = selection.Nudge(NudgeDirection.Down, 10, Desktop);

        Assert.Equal(selection, left);
        Assert.Equal(new PhysicalRect(-100, -90, 20, 20), down.Bounds);
    }

    [Theory]
    [InlineData(97, 97, SelectionHandle.TopLeft)]
    [InlineData(125, 98, SelectionHandle.Top)]
    [InlineData(152, 125, SelectionHandle.Right)]
    [InlineData(125, 152, SelectionHandle.Bottom)]
    [InlineData(75, 75, SelectionHandle.None)]
    public void HitTestHandle_UsesCornersBeforeEdges(int x, int y, SelectionHandle expected)
    {
        var selection = new SelectionState(new PhysicalRect(100, 100, 50, 50));

        Assert.Equal(expected, selection.HitTestHandle(new PhysicalPoint(x, y), 3));
    }
}
