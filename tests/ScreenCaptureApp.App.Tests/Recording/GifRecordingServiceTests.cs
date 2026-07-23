using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ScreenCaptureApp.App.Recording;

namespace ScreenCaptureApp.App.Tests.Recording;

public sealed class GifRecordingServiceTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        $"SnipArc.GifRecording.Tests.{Guid.NewGuid():N}");

    [Fact]
    public async Task EncoderWritesMultipleTimedFrames()
    {
        Directory.CreateDirectory(_root);
        string path = Path.Combine(_root, "recording.gif");
        GifFrameData first = GifRecordingService.CreateFrame(
            CreateSolidFrame(255, 0, 0),
            TimeSpan.FromMilliseconds(125));
        GifFrameData second = GifRecordingService.CreateFrame(
            CreateSolidFrame(0, 0, 255),
            TimeSpan.FromMilliseconds(125));

        await GifRecordingService.EncodeAtomicallyAsync([first, second], path);

        await using FileStream stream = File.OpenRead(path);
        GifBitmapDecoder decoder = new(
            stream,
            BitmapCreateOptions.PreservePixelFormat,
            BitmapCacheOption.OnLoad);
        Assert.Equal(2, decoder.Frames.Count);
        BitmapMetadata metadata = Assert.IsType<BitmapMetadata>(decoder.Frames[0].Metadata);
        Assert.InRange((ushort)metadata.GetQuery("/grctlext/Delay"), (ushort)12, (ushort)13);
    }

    private static BitmapSource CreateSolidFrame(byte red, byte green, byte blue)
    {
        const int width = 12;
        const int height = 8;
        byte[] pixels = new byte[width * height * 4];
        for (int index = 0; index < pixels.Length; index += 4)
        {
            pixels[index] = blue;
            pixels[index + 1] = green;
            pixels[index + 2] = red;
            pixels[index + 3] = 255;
        }

        BitmapSource image = BitmapSource.Create(
            width,
            height,
            96,
            96,
            PixelFormats.Bgra32,
            null,
            pixels,
            width * 4);
        image.Freeze();
        return image;
    }

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }
}
