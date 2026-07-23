using ScreenCaptureApp.Core.Geometry;
using ScreenCaptureApp.Windows.Windowing;

namespace ScreenCaptureApp.Windows.Tests.Windows;

public sealed class WindowCandidateFilterTests
{
    private static readonly PhysicalRect Bounds = new(10, 20, 800, 600);

    [Fact]
    public void EligibleVisibleApplicationWindowIsIncluded()
    {
        var facts = new WindowCandidateFacts(true, false, false, false, false, 100, "Document", Bounds);

        Assert.True(WindowCandidateFilter.IsEligible(facts, 200));
    }

    [Theory]
    [InlineData(false, false, false, false, false)]
    [InlineData(true, true, false, false, false)]
    [InlineData(true, false, true, false, false)]
    [InlineData(true, false, false, true, false)]
    [InlineData(true, false, false, false, true)]
    public void NonInteractiveWindowsAreExcluded(bool visible, bool minimized, bool cloaked, bool toolWindow, bool shellWindow)
    {
        var facts = new WindowCandidateFacts(visible, minimized, cloaked, toolWindow, shellWindow, 100, "Document", Bounds);

        Assert.False(WindowCandidateFilter.IsEligible(facts, 200));
    }

    [Fact]
    public void CurrentProcessWindowIsExcluded()
    {
        var facts = new WindowCandidateFacts(true, false, false, false, false, 200, "Screen Capture", Bounds);

        Assert.False(WindowCandidateFilter.IsEligible(facts, 200));
    }

    [Theory]
    [InlineData(0, "Document", 800, 600)]
    [InlineData(100, "", 800, 600)]
    [InlineData(100, "   ", 800, 600)]
    [InlineData(100, "Document", 0, 600)]
    [InlineData(100, "Document", 800, 0)]
    public void InvalidIdentityOrBoundsAreExcluded(uint processId, string title, int width, int height)
    {
        var facts = new WindowCandidateFacts(true, false, false, false, false, processId, title, new PhysicalRect(0, 0, width, height));

        Assert.False(WindowCandidateFilter.IsEligible(facts, 200));
    }
}
