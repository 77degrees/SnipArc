using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ScreenCaptureApp.Core.Displays;
using ScreenCaptureApp.Core.Geometry;
using ScreenCaptureApp.Core.Scrolling;
using ScreenCaptureApp.Windows.Capture;

namespace ScreenCaptureApp.App.Scrolling;

internal sealed record ScrollingCaptureResult(
    string Path,
    int PageCount,
    int PixelHeight,
    IReadOnlyList<int> Overlaps);

internal sealed class ScrollingCaptureService
{
    private readonly GdiScreenCaptureBackend _captureBackend;
    private readonly IDisplayTopologyProvider _topology;

    internal ScrollingCaptureService(
        GdiScreenCaptureBackend captureBackend,
        IDisplayTopologyProvider topology)
    {
        _captureBackend = captureBackend;
        _topology = topology;
    }

    internal PixelFrame CapturePage(PhysicalRect bounds)
    {
        DesktopCapture desktop = _captureBackend.CaptureVirtualDesktop(_topology.GetCurrent(), includeCursor: false);
        PhysicalRect clipped = bounds.Intersect(desktop.Bounds);
        if (clipped.IsEmpty || clipped.Width != bounds.Width || clipped.Height != bounds.Height)
        {
            throw new InvalidOperationException("The scrolling capture area is no longer fully visible.");
        }

        CroppedBitmap crop = new(
            desktop.Image,
            new Int32Rect(
                clipped.Left - desktop.Bounds.Left,
                clipped.Top - desktop.Bounds.Top,
                clipped.Width,
                clipped.Height));
        BitmapSource bgra = crop.Format == PixelFormats.Bgra32
            ? crop
            : new FormatConvertedBitmap(crop, PixelFormats.Bgra32, null, 0);
        int stride = checked(bgra.PixelWidth * 4);
        byte[] pixels = new byte[checked(stride * bgra.PixelHeight)];
        bgra.CopyPixels(pixels, stride, 0);
        return new PixelFrame(bgra.PixelWidth, bgra.PixelHeight, pixels);
    }

    internal static async Task<ScrollingCaptureResult> StitchAndSaveAsync(
        IReadOnlyList<PixelFrame> pages,
        string path)
    {
        ArgumentNullException.ThrowIfNull(pages);
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        if (pages.Count == 0) throw new ArgumentException("Capture at least one page.", nameof(pages));
        if (pages.Count > 30) throw new ArgumentException("A scrolling capture is limited to 30 pages.", nameof(pages));

        int minimumOverlap = Math.Clamp(pages[0].Height / 20, 8, 48);
        VerticalStitchResult stitched = VerticalFrameStitcher.Stitch(pages, minimumOverlap);
        if (stitched.Image.Height > 100_000)
        {
            throw new InvalidOperationException("The stitched capture exceeds the 100,000-pixel height limit.");
        }
        BitmapSource image = BitmapSource.Create(
            stitched.Image.Width,
            stitched.Image.Height,
            96,
            96,
            PixelFormats.Bgra32,
            null,
            stitched.Image.Pixels,
            stitched.Image.Width * 4);
        image.Freeze();

        string? directory = Path.GetDirectoryName(path);
        if (string.IsNullOrWhiteSpace(directory)) throw new IOException("The scrolling capture destination is invalid.");
        Directory.CreateDirectory(directory);
        string temporary = Path.Combine(directory, $".{Path.GetFileName(path)}.{Guid.NewGuid():N}.partial");
        PngBitmapEncoder encoder = new();
        encoder.Frames.Add(BitmapFrame.Create(image));
        try
        {
            await using (FileStream stream = new(
                temporary,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                81920,
                FileOptions.Asynchronous | FileOptions.WriteThrough))
            {
                encoder.Save(stream);
                await stream.FlushAsync().ConfigureAwait(false);
                stream.Flush(flushToDisk: true);
            }
            File.Move(temporary, path, overwrite: false);
        }
        catch
        {
            try { File.Delete(temporary); } catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
            throw;
        }

        return new ScrollingCaptureResult(path, pages.Count, stitched.Image.Height, stitched.Overlaps);
    }
}
