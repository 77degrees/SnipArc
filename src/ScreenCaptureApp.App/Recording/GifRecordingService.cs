using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
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

internal sealed record GifSpoolFrame(
    string Path,
    int DelayHundredths);

internal sealed class GifRecordingService
{
    private static readonly byte[] GifLoopExtension =
    [
        0x21, 0xFF, 0x0B,
        (byte)'N', (byte)'E', (byte)'T', (byte)'S', (byte)'C', (byte)'A',
        (byte)'P', (byte)'E', (byte)'2', (byte)'.', (byte)'0',
        0x03, 0x01, 0x00, 0x00, 0x00
    ];

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
        string spoolDirectory = $"{temporary}.frames";
        Directory.CreateDirectory(spoolDirectory);
        DateTimeOffset started = DateTimeOffset.UtcNow;
        using PeriodicTimer timer = new(options.FrameInterval);
        List<GifSpoolFrame> frames = [];
        int frameCount = 0;
        try
        {
            do
            {
                BitmapSource captured = CaptureRegion(bounds, options.MaximumDimension);
                GifFrameData frame = CreateFrame(captured, options.FrameInterval);
                string framePath = Path.Combine(spoolDirectory, $"{frameCount:D5}.png");
                WriteSpoolFrame(frame, framePath);
                frames.Add(new GifSpoolFrame(framePath, frame.DelayHundredths));
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

            await EncodeSpoolAsync(frames, temporary).ConfigureAwait(false);
            File.Move(temporary, path, overwrite: false);
            return frameCount;
        }
        catch
        {
            try { File.Delete(temporary); } catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
            throw;
        }
        finally
        {
            DeleteSpoolDirectory(spoolDirectory);
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
        string spoolDirectory = $"{temporary}.frames";
        Directory.CreateDirectory(spoolDirectory);
        try
        {
            List<GifSpoolFrame> spoolFrames = new(frames.Count);
            for (int index = 0; index < frames.Count; index++)
            {
                string framePath = Path.Combine(spoolDirectory, $"{index:D5}.png");
                WriteSpoolFrame(frames[index], framePath);
                spoolFrames.Add(new GifSpoolFrame(framePath, frames[index].DelayHundredths));
            }

            await EncodeSpoolAsync(spoolFrames, temporary).ConfigureAwait(false);
            File.Move(temporary, path, overwrite: false);
        }
        catch
        {
            try { File.Delete(temporary); } catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
            throw;
        }
        finally
        {
            DeleteSpoolDirectory(spoolDirectory);
        }
    }

    private static void WriteSpoolFrame(GifFrameData frame, string path)
    {
        BitmapSource source = BitmapSource.Create(
            frame.Width,
            frame.Height,
            96,
            96,
            PixelFormats.Bgra32,
            null,
            frame.Pixels,
            checked(frame.Width * 4));
        source.Freeze();

        PngBitmapEncoder encoder = new();
        encoder.Frames.Add(BitmapFrame.Create(source));
        using FileStream stream = new(
            path,
            FileMode.CreateNew,
            FileAccess.Write,
            FileShare.None,
            81920,
            FileOptions.WriteThrough);
        encoder.Save(stream);
        stream.Flush(flushToDisk: true);
    }

    private static async Task EncodeSpoolAsync(
        List<GifSpoolFrame> frames,
        string outputPath)
    {
        string wicOutputPath = $"{outputPath}.wic";
        GifBitmapEncoder encoder = new();
        List<FileStream> inputStreams = new(frames.Count);
        try
        {
            for (int index = 0; index < frames.Count; index++)
            {
                GifSpoolFrame frame = frames[index];
                FileStream input = new(
                    frame.Path,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.Read,
                    81920,
                    FileOptions.SequentialScan);
                inputStreams.Add(input);
                BitmapDecoder decoder = BitmapDecoder.Create(
                    input,
                    BitmapCreateOptions.PreservePixelFormat,
                    BitmapCacheOption.None);
                encoder.Frames.Add(BitmapFrame.Create(decoder.Frames[0]));
            }

            await using (FileStream output = new(
                wicOutputPath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                81920,
                FileOptions.Asynchronous | FileOptions.WriteThrough))
            {
                encoder.Save(output);
                await output.FlushAsync(CancellationToken.None).ConfigureAwait(false);
                output.Flush(flushToDisk: true);
            }

            RewriteGifWithTiming(wicOutputPath, outputPath, frames);
        }
        finally
        {
            foreach (FileStream input in inputStreams)
            {
                input.Dispose();
            }

            try { File.Delete(wicOutputPath); } catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
        }
    }

    private static void RewriteGifWithTiming(
        string inputPath,
        string outputPath,
        List<GifSpoolFrame> frames)
    {
        using FileStream input = new(
            inputPath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            81920,
            FileOptions.SequentialScan);
        using FileStream output = new(
            outputPath,
            FileMode.CreateNew,
            FileAccess.Write,
            FileShare.None,
            81920,
            FileOptions.WriteThrough);

        Span<byte> headerAndLogicalScreen = stackalloc byte[13];
        input.ReadExactly(headerAndLogicalScreen);
        if (!headerAndLogicalScreen[..3].SequenceEqual("GIF"u8))
        {
            throw new InvalidDataException("The WIC encoder did not produce a GIF stream.");
        }

        output.Write(headerAndLogicalScreen);
        byte logicalScreenFlags = headerAndLogicalScreen[10];
        if ((logicalScreenFlags & 0x80) != 0)
        {
            int globalColorTableBytes = 3 * (1 << ((logicalScreenFlags & 0x07) + 1));
            CopyBytes(input, output, globalColorTableBytes);
        }

        output.Write(GifLoopExtension);
        int frameIndex = 0;
        while (true)
        {
            int blockType = ReadRequiredByte(input);
            switch (blockType)
            {
                case 0x21:
                {
                    int extensionType = ReadRequiredByte(input);
                    if (extensionType == 0xF9)
                    {
                        SkipGraphicControlExtension(input);
                    }
                    else
                    {
                        output.WriteByte(0x21);
                        output.WriteByte((byte)extensionType);
                        CopySubBlocks(input, output);
                    }

                    break;
                }

                case 0x2C:
                {
                    if (frameIndex >= frames.Count)
                    {
                        throw new InvalidDataException("The GIF contains more images than captured frames.");
                    }

                    WriteGraphicControlExtension(output, frames[frameIndex].DelayHundredths);
                    output.WriteByte(0x2C);
                    Span<byte> imageDescriptor = stackalloc byte[9];
                    input.ReadExactly(imageDescriptor);
                    output.Write(imageDescriptor);
                    if ((imageDescriptor[8] & 0x80) != 0)
                    {
                        int localColorTableBytes = 3 * (1 << ((imageDescriptor[8] & 0x07) + 1));
                        CopyBytes(input, output, localColorTableBytes);
                    }

                    output.WriteByte((byte)ReadRequiredByte(input));
                    CopySubBlocks(input, output);
                    frameIndex++;
                    break;
                }

                case 0x3B:
                    if (frameIndex != frames.Count)
                    {
                        throw new InvalidDataException("The GIF contains fewer images than captured frames.");
                    }

                    output.WriteByte(0x3B);
                    output.Flush(flushToDisk: true);
                    return;

                default:
                    throw new InvalidDataException(
                        $"Unexpected GIF block introducer 0x{blockType:X2}.");
            }
        }
    }

    private static void WriteGraphicControlExtension(
        FileStream output,
        int delayHundredths)
    {
        ushort delay = (ushort)Math.Clamp(delayHundredths, 1, ushort.MaxValue);
        Span<byte> extension = stackalloc byte[8]
        {
            0x21,
            0xF9,
            0x04,
            0x04,
            (byte)(delay & 0xFF),
            (byte)(delay >> 8),
            0x00,
            0x00
        };
        output.Write(extension);
    }

    private static void SkipGraphicControlExtension(FileStream input)
    {
        int blockSize = ReadRequiredByte(input);
        if (blockSize != 4)
        {
            throw new InvalidDataException("The GIF contains an invalid graphic control extension.");
        }

        Span<byte> payloadAndTerminator = stackalloc byte[5];
        input.ReadExactly(payloadAndTerminator);
        if (payloadAndTerminator[4] != 0)
        {
            throw new InvalidDataException("The GIF graphic control extension is not terminated.");
        }
    }

    private static void CopySubBlocks(FileStream input, FileStream output)
    {
        while (true)
        {
            int length = ReadRequiredByte(input);
            output.WriteByte((byte)length);
            if (length == 0) return;
            CopyBytes(input, output, length);
        }
    }

    private static void CopyBytes(FileStream input, FileStream output, int count)
    {
        Span<byte> buffer = stackalloc byte[768];
        int remaining = count;
        while (remaining > 0)
        {
            int chunk = Math.Min(remaining, buffer.Length);
            input.ReadExactly(buffer[..chunk]);
            output.Write(buffer[..chunk]);
            remaining -= chunk;
        }
    }

    private static int ReadRequiredByte(FileStream input)
    {
        int value = input.ReadByte();
        return value >= 0
            ? value
            : throw new EndOfStreamException("The GIF stream ended unexpectedly.");
    }

    private static void DeleteSpoolDirectory(string path)
    {
        try
        {
            Directory.Delete(path, recursive: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
        }
    }
}
