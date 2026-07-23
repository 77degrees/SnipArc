using System.ComponentModel;
using System.Runtime.InteropServices;
using ScreenCaptureApp.Core.Displays;
using ScreenCaptureApp.Core.Geometry;
using ScreenCaptureApp.Windows.Interop;

namespace ScreenCaptureApp.Windows.Displays;

public sealed class DisplayTopologyService : IDisplayTopologyProvider
{
    private const uint DefaultDpi = 96;

    public DisplayTopology GetCurrent()
    {
        List<DisplayInfo> displays = [];
        Win32Exception? callbackError = null;
        NativeMethods.MonitorEnumProcedure callback = (
            nint monitor,
            nint deviceContext,
            ref NativeRect monitorRectangle,
            nint data) =>
        {
            MonitorInfoEx monitorInfo = new()
            {
                Size = (uint)Marshal.SizeOf<MonitorInfoEx>(),
                DeviceName = string.Empty
            };

            if (!NativeMethods.GetMonitorInfo(monitor, ref monitorInfo))
            {
                callbackError = new Win32Exception(
                    Marshal.GetLastWin32Error(),
                    "Could not read monitor information.");
                return false;
            }

            uint dpiX = DefaultDpi;
            uint dpiY = DefaultDpi;
            _ = NativeMethods.GetDpiForMonitor(monitor, MonitorDpiType.Effective, out dpiX, out dpiY);
            if (dpiX == 0 || dpiY == 0)
            {
                dpiX = DefaultDpi;
                dpiY = DefaultDpi;
            }

            displays.Add(new DisplayInfo(
                monitorInfo.DeviceName,
                ToPhysicalRect(monitorInfo.Monitor),
                ToPhysicalRect(monitorInfo.WorkArea),
                dpiX,
                dpiY,
                (monitorInfo.Flags & NativeMethods.MonitorInfoFlagPrimary) != 0));
            return true;
        };

        bool completed = NativeMethods.EnumDisplayMonitors(0, 0, callback, 0);
        if (callbackError is not null)
        {
            throw callbackError;
        }

        if (!completed)
        {
            throw new Win32Exception(Marshal.GetLastWin32Error(), "Could not enumerate displays.");
        }

        return new DisplayTopology(displays);
    }

    private static PhysicalRect ToPhysicalRect(NativeRect rectangle) =>
        PhysicalRect.FromEdges(rectangle.Left, rectangle.Top, rectangle.Right, rectangle.Bottom);
}
