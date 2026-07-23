using ScreenCaptureApp.Windows.Hotkeys;

namespace ScreenCaptureApp.Windows.Tests.Hotkeys;

public sealed class WindowsSnippingShortcutStateTests
{
    private const uint LeftWindows = 0x5B;
    private const uint RightWindows = 0x5C;
    private const uint LeftShift = 0xA0;
    private const uint RightShift = 0xA1;
    private const uint Control = 0x11;
    private const uint S = 0x53;

    [Theory]
    [InlineData(LeftWindows, LeftShift)]
    [InlineData(LeftWindows, RightShift)]
    [InlineData(RightWindows, LeftShift)]
    public void Process_WindowsShiftS_SuppressesAndTriggers(uint windowsKey, uint shiftKey)
    {
        var state = new WindowsSnippingShortcutState();

        state.Process(windowsKey, true);
        state.Process(shiftKey, true);
        var result = state.Process(S, true);

        Assert.True(result.Suppress);
        Assert.True(result.Triggered);
    }

    [Fact]
    public void Process_CtrlShiftS_DoesNotIntercept()
    {
        var state = new WindowsSnippingShortcutState();

        state.Process(Control, true);
        state.Process(LeftShift, true);
        var result = state.Process(S, true);

        Assert.False(result.Suppress);
        Assert.False(result.Triggered);
    }

    [Fact]
    public void Process_RepeatedKeyDown_TriggersOnlyOnce()
    {
        var state = PressModifiers();

        var first = state.Process(S, true);
        var repeated = state.Process(S, true);

        Assert.True(first.Triggered);
        Assert.True(repeated.Suppress);
        Assert.False(repeated.Triggered);
    }

    [Fact]
    public void Process_KeyUp_IsSuppressedAndAllowsNextPress()
    {
        var state = PressModifiers();
        state.Process(S, true);

        var keyUp = state.Process(S, false);
        var nextPress = state.Process(S, true);

        Assert.True(keyUp.Suppress);
        Assert.False(keyUp.Triggered);
        Assert.True(nextPress.Suppress);
        Assert.True(nextPress.Triggered);
    }

    [Fact]
    public void Reset_ClearsPressedModifiers()
    {
        var state = PressModifiers();
        state.Reset();

        var result = state.Process(S, true);

        Assert.False(result.Suppress);
        Assert.False(result.Triggered);
    }

    private static WindowsSnippingShortcutState PressModifiers()
    {
        var state = new WindowsSnippingShortcutState();
        state.Process(LeftWindows, true);
        state.Process(LeftShift, true);
        return state;
    }
}
