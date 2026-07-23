using System.IO;
using System.Windows.Media.Imaging;
using ScreenCaptureApp.App.Scrolling;
using ScreenCaptureApp.Core.Scrolling;

namespace ScreenCaptureApp.App.Tests.Scrolling;

public sealed class ScrollingCaptureServiceTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        $"SnipArc.ScrollingCapture.Tests.{Guid.NewGuid():N}");

    [Fact]
    public async Task StitchAndSaveWritesDecodablePng()
    {
        Directory.CreateDirectory(_root);
        string path = Path.Combine(_root, "scrolling.png");
        PixelFrame page = CreatePage(width: 20, height: 30);

        ScrollingCaptureResult result =
            await ScrollingCaptureService.StitchAndSaveAsync([page], path);

        Assert.Equal(1, result.PageCount);
        Assert.Equal(30, result.PixelHeight);
        await using FileStream stream = File.OpenRead(path);
        PngBitmapDecoder decoder = new(
            stream,
            BitmapCreateOptions.PreservePixelFormat,
            BitmapCacheOption.OnLoad);
        Assert.Equal(20, decoder.Frames[0].PixelWidth);
        Assert.Equal(30, decoder.Frames[0].PixelHeight);
    }

    [Fact]
    public async Task StitchAndSaveRejectsMoreThanThirtyPages()
    {
        PixelFrame page = CreatePage(width: 4, height: 4);
        PixelFrame[] pages = Enumerable.Repeat(page, 31).ToArray();

        ArgumentException error = await Assert.ThrowsAsync<ArgumentException>(() =>
            ScrollingCaptureService.StitchAndSaveAsync(
                pages,
                Path.Combine(_root, "too-many.png")));

        Assert.Contains("30 pages", error.Message, StringComparison.Ordinal);
    }

    private static PixelFrame CreatePage(int width, int height)
    {
        byte[] pixels = new byte[width * height * 4];
        for (int index = 0; index < pixels.Length; index += 4)
        {
            pixels[index] = 40;
            pixels[index + 1] = 80;
            pixels[index + 2] = 120;
            pixels[index + 3] = 255;
        }
        return new PixelFrame(width, height, pixels);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }
}
