using Microsoft.Win32.SafeHandles;

namespace ScreenCaptureApp.Windows.Interop;

internal sealed class SafeMemoryDeviceContext : SafeHandleZeroOrMinusOneIsInvalid
{
    public SafeMemoryDeviceContext() : base(ownsHandle: true)
    {
    }

    protected override bool ReleaseHandle() => NativeMethods.DeleteDC(handle);
}

internal sealed class SafeGdiObject : SafeHandleZeroOrMinusOneIsInvalid
{
    public SafeGdiObject() : base(ownsHandle: true)
    {
    }

    protected override bool ReleaseHandle() => NativeMethods.DeleteObject(handle);
}

internal sealed class SafeIcon : SafeHandleZeroOrMinusOneIsInvalid
{
    public SafeIcon() : base(ownsHandle: true)
    {
    }

    protected override bool ReleaseHandle() => NativeMethods.DestroyIcon(handle);
}
