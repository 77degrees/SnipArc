using System.ComponentModel;
using System.Windows.Interop;
using ScreenCaptureApp.Windows.Interop;

namespace ScreenCaptureApp.Windows.Hotkeys;

[Flags]
public enum HotkeyModifiers : uint
{
    None = 0,
    Alt = 0x0001,
    Control = 0x0002,
    Shift = 0x0004,
    Windows = 0x0008,
    NoRepeat = 0x4000
}

public readonly record struct HotkeyGesture(uint VirtualKey, HotkeyModifiers Modifiers = HotkeyModifiers.None)
{
    public bool IsValid => VirtualKey is > 0 and <= 0xFE;
}

public sealed class GlobalHotkeyService : IDisposable
{
    private const int WmHotkey = 0x0312;
    private const int PrimaryHotkeyId = 0x5343;
    private const int SecondaryHotkeyId = 0x5344;
    private readonly HwndSource _source;
    private bool _isRegistered;
    private int _registeredId;
    private bool _disposed;

    public GlobalHotkeyService(HwndSource source)
    {
        _source = source ?? throw new ArgumentNullException(nameof(source));
        _source.AddHook(WindowProcedure);
    }

    public event EventHandler? HotkeyPressed;

    public HotkeyGesture? RegisteredGesture { get; private set; }

    public bool TryRegister(HotkeyGesture gesture, out string? error)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (!gesture.IsValid)
        {
            error = "Choose a valid keyboard key.";
            return false;
        }

        if (_isRegistered && RegisteredGesture == gesture)
        {
            error = null;
            return true;
        }

        var candidateId = _registeredId == PrimaryHotkeyId ? SecondaryHotkeyId : PrimaryHotkeyId;
        if (!NativeMethods.RegisterHotKey(
                _source.Handle,
                candidateId,
                (uint)(gesture.Modifiers | HotkeyModifiers.NoRepeat),
                gesture.VirtualKey))
        {
            int nativeError = System.Runtime.InteropServices.Marshal.GetLastWin32Error();
            error = nativeError == 1409
                ? "That shortcut is already being used by Windows or another application."
                : new Win32Exception(nativeError, "The global shortcut could not be registered.").Message;
            return false;
        }

        if (_isRegistered)
        {
            _ = NativeMethods.UnregisterHotKey(_source.Handle, _registeredId);
        }

        _isRegistered = true;
        _registeredId = candidateId;
        RegisteredGesture = gesture;
        error = null;
        return true;
    }

    public void Unregister()
    {
        if (_isRegistered)
        {
            _ = NativeMethods.UnregisterHotKey(_source.Handle, _registeredId);
            _isRegistered = false;
            _registeredId = 0;
            RegisteredGesture = null;
        }
    }

    private nint WindowProcedure(nint windowHandle, int message, nint wordParameter, nint longParameter, ref bool handled)
    {
        if (message == WmHotkey && wordParameter == _registeredId)
        {
            handled = true;
            HotkeyPressed?.Invoke(this, EventArgs.Empty);
        }

        return 0;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        Unregister();
        _source.RemoveHook(WindowProcedure);
        _disposed = true;
    }
}
