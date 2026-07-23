using System.ComponentModel;
using System.Runtime.InteropServices;
using ScreenCaptureApp.Windows.Interop;

namespace ScreenCaptureApp.Windows.Hotkeys;

public sealed class WindowsSnippingShortcutService : IDisposable
{
    private const int WhKeyboardLowLevel = 13;
    private const int WmKeyDown = 0x0100;
    private const int WmKeyUp = 0x0101;
    private const int WmSystemKeyDown = 0x0104;
    private const int WmSystemKeyUp = 0x0105;
    private readonly WindowsSnippingShortcutState _state = new();
    private NativeMethods.LowLevelKeyboardProcedure? _procedure;
    private nint _hook;
    private bool _disposed;

    public event EventHandler? ShortcutPressed;

    public bool IsEnabled => _hook != 0;

    public bool TryEnable(out string? error)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (IsEnabled)
        {
            error = null;
            return true;
        }

        _procedure = KeyboardProcedure;
        var module = NativeMethods.GetModuleHandle(null);
        _hook = NativeMethods.SetWindowsHookEx(WhKeyboardLowLevel, _procedure, module, 0);
        if (_hook != 0)
        {
            error = null;
            return true;
        }

        var nativeError = Marshal.GetLastWin32Error();
        _procedure = null;
        error = new Win32Exception(nativeError, "Windows + Shift + S could not be intercepted.").Message;
        return false;
    }

    public void Disable()
    {
        if (_hook != 0)
        {
            _ = NativeMethods.UnhookWindowsHookEx(_hook);
            _hook = 0;
        }

        _state.Reset();
        _procedure = null;
    }

    private nint KeyboardProcedure(int code, nint wordParameter, nint longParameter)
    {
        if (code >= 0 && IsKeyboardMessage(wordParameter))
        {
            var data = Marshal.PtrToStructure<LowLevelKeyboardData>(longParameter);
            var isKeyDown = wordParameter == WmKeyDown || wordParameter == WmSystemKeyDown;
            var result = _state.Process(data.VirtualKey, isKeyDown);
            if (result.Triggered) ShortcutPressed?.Invoke(this, EventArgs.Empty);
            if (result.Suppress) return 1;
        }

        return NativeMethods.CallNextHookEx(_hook, code, wordParameter, longParameter);
    }

    private static bool IsKeyboardMessage(nint message) =>
        message == WmKeyDown || message == WmKeyUp || message == WmSystemKeyDown || message == WmSystemKeyUp;

    public void Dispose()
    {
        if (_disposed) return;
        Disable();
        _disposed = true;
    }
}

internal readonly record struct ShortcutHookResult(bool Suppress, bool Triggered);

internal sealed class WindowsSnippingShortcutState
{
    private const uint VkShift = 0x10;
    private const uint VkLeftShift = 0xA0;
    private const uint VkRightShift = 0xA1;
    private const uint VkLeftWindows = 0x5B;
    private const uint VkRightWindows = 0x5C;
    private const uint VkS = 0x53;
    private readonly HashSet<uint> _pressedKeys = [];
    private bool _sIsSuppressed;

    internal ShortcutHookResult Process(uint virtualKey, bool isKeyDown)
    {
        if (isKeyDown) _pressedKeys.Add(virtualKey);
        else _pressedKeys.Remove(virtualKey);

        if (virtualKey != VkS) return default;

        if (isKeyDown && HasWindowsKey && HasShift)
        {
            var firstPress = !_sIsSuppressed;
            _sIsSuppressed = true;
            return new ShortcutHookResult(true, firstPress);
        }

        if (!isKeyDown && _sIsSuppressed)
        {
            _sIsSuppressed = false;
            return new ShortcutHookResult(true, false);
        }

        return default;
    }

    internal void Reset()
    {
        _pressedKeys.Clear();
        _sIsSuppressed = false;
    }

    private bool HasWindowsKey => _pressedKeys.Contains(VkLeftWindows) || _pressedKeys.Contains(VkRightWindows);
    private bool HasShift => _pressedKeys.Contains(VkShift) || _pressedKeys.Contains(VkLeftShift) || _pressedKeys.Contains(VkRightShift);
}
