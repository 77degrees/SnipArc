using ScreenCaptureApp.Core.Geometry;
using ScreenCaptureApp.Windows.Capture;

namespace ScreenCaptureApp.Windows.Tests.Capture;

public sealed class CursorCompositionTests
{
    [Fact]
    public void DrawPosition_AccountsForHotspotAndNegativeVirtualOrigin()
    {
        PhysicalRect captureBounds = new(-1920, -200, 4480, 1640);

        PhysicalPoint result = GdiScreenCaptureBackend.GetCursorDrawPosition(
            new PhysicalPoint(-100, 50),
            new PhysicalPoint(4, 7),
            captureBounds);

        Assert.Equal(new PhysicalPoint(1816, 243), result);
    }
}
