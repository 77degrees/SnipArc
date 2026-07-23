using ScreenCaptureApp.Windows.Hotkeys;

namespace ScreenCaptureApp.Windows.Tests.Hotkeys;

public sealed class HotkeyGestureTests
{
    [Theory]
    [InlineData(0, false)]
    [InlineData(0x2C, true)]
    [InlineData(0xFE, true)]
    [InlineData(0xFF, false)]
    public void IsValid_ValidatesVirtualKeyRange(uint virtualKey, bool expected)
    {
        Assert.Equal(expected, new HotkeyGesture(virtualKey).IsValid);
    }
}
