using System.ComponentModel;
using System.Runtime.InteropServices;
using ScreenCaptureApp.Core.Geometry;
using ScreenCaptureApp.Windows.Interop;

namespace ScreenCaptureApp.Windows.Windowing;

public sealed record DetectedWindow(nint Handle, string Title, PhysicalRect Bounds, uint ProcessId);

public interface IWindowDetectionService
{
    public IReadOnlyList<DetectedWindow> GetVisibleWindows(uint excludedProcessId);
}

public sealed class WindowDetectionService : IWindowDetectionService
{
    public IReadOnlyList<DetectedWindow> GetVisibleWindows(uint excludedProcessId)
    {
        List<DetectedWindow> windows = [];
        var shellWindow = NativeMethods.GetShellWindow();

        NativeMethods.WindowEnumProcedure callback = (windowHandle, _) =>
        {
            try
            {
                var facts = ReadFacts(windowHandle, shellWindow);
                if (!WindowCandidateFilter.IsEligible(facts, excludedProcessId)) return true;

                windows.Add(new DetectedWindow(windowHandle, facts.Title.Trim(), facts.Bounds, facts.ProcessId));
            }
            catch (Exception ex) when (ex is Win32Exception or OverflowException or ArgumentException)
            {
                // A window may disappear while the z-order snapshot is being enumerated.
            }

            return true;
        };

        if (!NativeMethods.EnumWindows(callback, 0))
        {
            throw new Win32Exception(Marshal.GetLastWin32Error(), "Could not enumerate desktop windows.");
        }

        return windows;
    }

    private static WindowCandidateFacts ReadFacts(nint windowHandle, nint shellWindow)
    {
        _ = NativeMethods.GetWindowThreadProcessId(windowHandle, out var processId);
        var titleLength = NativeMethods.GetWindowTextLength(windowHandle);
        var titleBuffer = new char[Math.Max(1, titleLength + 1)];
        var copiedCharacters = NativeMethods.GetWindowText(windowHandle, titleBuffer, titleBuffer.Length);

        _ = NativeMethods.DwmGetWindowAttribute(
            windowHandle,
            NativeMethods.DwmwaCloaked,
            out int cloaked,
            sizeof(int));

        var exStyle = NativeMethods.GetWindowLongPtr(windowHandle, NativeMethods.GwlExstyle).ToInt64();
        var hasExtendedBounds = NativeMethods.DwmGetWindowAttribute(
            windowHandle,
            NativeMethods.DwmwaExtendedFrameBounds,
            out NativeRect rectangle,
            Marshal.SizeOf<NativeRect>()) == 0;
        if (!hasExtendedBounds && !NativeMethods.GetWindowRect(windowHandle, out rectangle))
        {
            rectangle = default;
        }

        var bounds = rectangle.Right > rectangle.Left && rectangle.Bottom > rectangle.Top
            ? PhysicalRect.FromEdges(rectangle.Left, rectangle.Top, rectangle.Right, rectangle.Bottom)
            : default;

        return new WindowCandidateFacts(
            NativeMethods.IsWindowVisible(windowHandle),
            NativeMethods.IsIconic(windowHandle),
            cloaked != 0,
            (exStyle & NativeMethods.WsExToolwindow) != 0,
            windowHandle == shellWindow,
            processId,
            copiedCharacters > 0 ? new string(titleBuffer, 0, copiedCharacters) : string.Empty,
            bounds);
    }
}

internal readonly record struct WindowCandidateFacts(
    bool IsVisible,
    bool IsMinimized,
    bool IsCloaked,
    bool IsToolWindow,
    bool IsShellWindow,
    uint ProcessId,
    string Title,
    PhysicalRect Bounds);

internal static class WindowCandidateFilter
{
    internal static bool IsEligible(WindowCandidateFacts facts, uint excludedProcessId) =>
        facts.IsVisible &&
        !facts.IsMinimized &&
        !facts.IsCloaked &&
        !facts.IsToolWindow &&
        !facts.IsShellWindow &&
        facts.ProcessId != 0 &&
        facts.ProcessId != excludedProcessId &&
        !string.IsNullOrWhiteSpace(facts.Title) &&
        !facts.Bounds.IsEmpty;
}
