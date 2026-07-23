using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media.Imaging;
using ScreenCaptureApp.Core.Displays;
using ScreenCaptureApp.Core.Geometry;
using ScreenCaptureApp.Windows.Interop;

namespace ScreenCaptureApp.Windows.Capture;

public sealed record DesktopCapture(BitmapSource Image, PhysicalRect Bounds);

public interface IScreenCaptureBackend
{
    public DesktopCapture CaptureVirtualDesktop(DisplayTopology topology, bool includeCursor);
}

public sealed class GdiScreenCaptureBackend : IScreenCaptureBackend
{
    public DesktopCapture CaptureVirtualDesktop(DisplayTopology topology, bool includeCursor)
    {
        ArgumentNullException.ThrowIfNull(topology);
        PhysicalRect bounds = topology.VirtualBounds;
        if (bounds.IsEmpty)
        {
            throw new ArgumentException("The display topology has empty virtual bounds.", nameof(topology));
        }

        nint desktopDeviceContext = NativeMethods.GetDC(0);
        if (desktopDeviceContext == 0)
        {
            throw new Win32Exception(Marshal.GetLastWin32Error(), "Could not open the desktop device context.");
        }

        try
        {
            using SafeMemoryDeviceContext memoryDeviceContext = NativeMethods.CreateCompatibleDC(desktopDeviceContext);
            if (memoryDeviceContext.IsInvalid)
            {
                throw new Win32Exception(Marshal.GetLastWin32Error(), "Could not create a compatible device context.");
            }

            using SafeGdiObject bitmap = NativeMethods.CreateCompatibleBitmap(desktopDeviceContext, bounds.Width, bounds.Height);
            if (bitmap.IsInvalid)
            {
                throw new Win32Exception(Marshal.GetLastWin32Error(), "Could not create the desktop bitmap.");
            }

            nint memoryHandle = memoryDeviceContext.DangerousGetHandle();
            nint bitmapHandle = bitmap.DangerousGetHandle();
            nint previousObject = NativeMethods.SelectObject(memoryHandle, bitmapHandle);
            if (previousObject == 0 || previousObject == -1)
            {
                throw new Win32Exception(Marshal.GetLastWin32Error(), "Could not select the desktop bitmap.");
            }

            try
            {
                if (!NativeMethods.BitBlt(
                        memoryHandle,
                        0,
                        0,
                        bounds.Width,
                        bounds.Height,
                        desktopDeviceContext,
                        bounds.Left,
                        bounds.Top,
                        NativeMethods.Srccopy | NativeMethods.Captureblt))
                {
                    throw new Win32Exception(Marshal.GetLastWin32Error(), "Desktop capture failed.");
                }

                if (includeCursor)
                {
                    ComposeCursor(memoryHandle, bounds);
                }

                BitmapSource image = Imaging.CreateBitmapSourceFromHBitmap(
                    bitmapHandle,
                    0,
                    Int32Rect.Empty,
                    BitmapSizeOptions.FromEmptyOptions());
                image.Freeze();
                return new DesktopCapture(image, bounds);
            }
            finally
            {
                _ = NativeMethods.SelectObject(memoryHandle, previousObject);
            }
        }
        finally
        {
            _ = NativeMethods.ReleaseDC(0, desktopDeviceContext);
        }
    }

    internal static PhysicalPoint GetCursorDrawPosition(
        PhysicalPoint cursorScreenPosition,
        PhysicalPoint hotspot,
        PhysicalRect captureBounds) =>
        new(
            checked(cursorScreenPosition.X - hotspot.X - captureBounds.Left),
            checked(cursorScreenPosition.Y - hotspot.Y - captureBounds.Top));

    private static void ComposeCursor(nint destinationDeviceContext, PhysicalRect captureBounds)
    {
        CursorInfo cursorInfo = new() { Size = (uint)Marshal.SizeOf<CursorInfo>() };
        if (!NativeMethods.GetCursorInfo(ref cursorInfo) ||
            (cursorInfo.Flags & NativeMethods.CursorShowing) == 0 ||
            cursorInfo.CursorHandle == 0)
        {
            return;
        }

        using SafeIcon cursorIcon = NativeMethods.CopyIcon(cursorInfo.CursorHandle);
        if (cursorIcon.IsInvalid || !NativeMethods.GetIconInfo(cursorIcon.DangerousGetHandle(), out IconInfo iconInfo))
        {
            return;
        }

        try
        {
            PhysicalPoint position = GetCursorDrawPosition(
                new PhysicalPoint(cursorInfo.ScreenPosition.X, cursorInfo.ScreenPosition.Y),
                new PhysicalPoint(checked((int)iconInfo.HotspotX), checked((int)iconInfo.HotspotY)),
                captureBounds);
            _ = NativeMethods.DrawIconEx(
                destinationDeviceContext,
                position.X,
                position.Y,
                cursorIcon.DangerousGetHandle(),
                0,
                0,
                0,
                0,
                NativeMethods.DiNormal);
        }
        finally
        {
            if (iconInfo.ColorBitmap != 0)
            {
                _ = NativeMethods.DeleteObject(iconInfo.ColorBitmap);
            }

            if (iconInfo.MaskBitmap != 0)
            {
                _ = NativeMethods.DeleteObject(iconInfo.MaskBitmap);
            }
        }
    }
}
