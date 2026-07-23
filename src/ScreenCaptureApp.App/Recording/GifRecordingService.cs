using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using KGySoft.Drawing.Imaging;
using ScreenCaptureApp.Core.Displays;
using ScreenCaptureApp.Core.Geometry;
using ScreenCaptureApp.Windows.Capture;

namespace ScreenCaptureApp.App.Recording;

internal sealed record GifRecordingOptions(
    TimeSpan FrameInterval,
    TimeSpan MaximumDuration,
    int MaximumDimension = 1280);

internal sealed record GifFrameData(
    byte[] Pixels,
    int Width,
    int Height,
    int DelayHundredths);

internal sealed class GifRecordingService
{
    private readonly GdiScreenCaptureBackend _captureBackend;
    private readonly IDisplayTopologyProvider _topology;

    internal GifRecordingService(
        GdiScreenCaptureBackend captureBackend,
        IDisplayTopologyProvider topology)
    {
        _captureBackend = captureBackend;
        _topology = topology;
    }

    internal async Task<int> RecordToFileAsync(
        PhysicalRect bounds,
        string path,
        GifRecordingOptions options,
        IProgress<TimeSpan>? progress,
        CancellationToken stopToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        if (bounds.IsEmpty) throw new ArgumentException("The recording bounds cannot be empty.", nameof(bounds));
        if (options.FrameInterval <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(options));
        if (options.MaximumDuration <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(options));

        string? directory = Path.GetDirectoryName(path);
        if (string.IsNullOrWhiteSpace(directory)) throw new IOException("The recording destination is invalid.");
        Directory.CreateDirectory(directory);
        string temporary = Path.Combine(directory, $".{Path.GetFileName(path)}.{Guid.NewGuid():N}.partial");
        DateTimeOffset started = DateTimeOffset.UtcNow;
        using PeriodicTimer timer = new(options.FrameInterval);
        int frameCount = 0;
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
                GifEncoder? encoder = null;
                try
                {
                    do
                    {
                        BitmapSource captured = CaptureRegion(bounds, options.MaximumDimension);
                        GifFrameData frame = CreateFrame(captured, options.FrameInterval);
                        encoder ??= CreateEncoder(stream, frame);
                        AddFrame(encoder, frame);
                        frameCount++;
                        TimeSpan elapsed = DateTimeOffset.UtcNow - started;
                        progress?.Report(elapsed);
                        if (elapsed >= options.MaximumDuration || stopToken.IsCancellationRequested) break;

                        try
                        {
                            if (!await timer.WaitForNextTickAsync(stopToken).ConfigureAwait(false)) break;
                        }
                        catch (OperationCanceledException) when (stopToken.IsCancellationRequested)
                        {
                            break;
                        }
                    }
                    while (true);
                }
                finally
                {
                    (encoder as IDisposable)?.Dispose();
                }

                await stream.FlushAsync(CancellationToken.None).ConfigureAwait(false);
                stream.Flush(flushToDisk: true);
            }
            File.Move(temporary, path, overwrite: false);
            return frameCount;
        }
        catch
        {
            try { File.Delete(temporary); } catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
            throw;
        }
    }

    private BitmapSource CaptureRegion(PhysicalRect bounds, int maximumDimension)
    {
        DesktopCapture desktop = _captureBackend.CaptureVirtualDesktop(_topology.GetCurrent(), includeCursor: false);
        PhysicalRect clipped = bounds.Intersect(desktop.Bounds);
        if (clipped.IsEmpty) throw new InvalidOperationException("The recording area is no longer visible.");

        CroppedBitmap crop = new(
            desktop.Image,
            new Int32Rect(
                clipped.Left - desktop.Bounds.Left,
                clipped.Top - desktop.Bounds.Top,
                clipped.Width,
                clipped.Height));
        crop.Freeze();
        return Downscale(crop, maximumDimension);
    }

    private static BitmapSource Downscale(BitmapSource source, int maximumDimension)
    {
        int largest = Math.Max(source.PixelWidth, source.PixelHeight);
        if (largest <= maximumDimension) return source;

        double scale = (double)maximumDimension / largest;
        TransformedBitmap transformed = new(source, new ScaleTransform(scale, scale));
        transformed.Freeze();
        return transformed;
    }

    internal static GifFrameData CreateFrame(BitmapSource image, TimeSpan interval)
    {
        BitmapSource bgra = image.Format == PixelFormats.Bgra32
            ? image
            : new FormatConvertedBitmap(image, PixelFormats.Bgra32, null, 0);
        int stride = checked(bgra.PixelWidth * 4);
        byte[] pixels = new byte[checked(stride * bgra.PixelHeight)];
        bgra.CopyPixels(pixels, stride, 0);
        return new GifFrameData(
            pixels,
            bgra.PixelWidth,
            bgra.PixelHeight,
            Math.Max(1, (int)Math.Round(
                interval.TotalMilliseconds / 10,
                MidpointRounding.AwayFromZero)));
    }

    internal static async Task EncodeAtomicallyAsync(IReadOnlyList<GifFrameData> frames, string path)
    {
        if (frames.Count == 0) throw new ArgumentException("At least one GIF frame is required.", nameof(frames));
        if (frames.Any(frame => frame.Width != frames[0].Width || frame.Height != frames[0].Height))
        {
            throw new ArgumentException("All GIF frames must have the same dimensions.", nameof(frames));
        }

        string? directory = Path.GetDirectoryName(path);
        if (string.IsNullOrWhiteSpace(directory)) throw new IOException("The recording destination is invalid.");
        Directory.CreateDirectory(directory);
        string temporary = Path.Combine(directory, $".{Path.GetFileName(path)}.{Guid.NewGuid():N}.partial");
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
                using (GifEncoder encoder = CreateEncoder(stream, frames[0]))
                {
                    foreach (GifFrameData frame in frames)
                    {
                        AddFrame(encoder, frame);
                    }
                }
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
    }

    private static GifEncoder CreateEncoder(Stream stream, GifFrameData firstFrame) =>
        new(stream, new System.Drawing.Size(firstFrame.Width, firstFrame.Height))
        {
            RepeatCount = 0,
            AddMetaInfo = false
        };

    private static void AddFrame(GifEncoder encoder, GifFrameData frame)
    {
        using IReadWriteBitmapData bitmap = BitmapDataFactory.CreateBitmapData(
            frame.Pixels,
            new System.Drawing.Size(frame.Width, frame.Height),
            frame.Width * 4,
            KnownPixelFormat.Format32bppArgb);
        encoder.AddImage(
            bitmap,
            System.Drawing.Point.Empty,
            frame.DelayHundredths,
            GifGraphicDisposalMethod.DoNotDispose);
    }
}
