using System.Runtime.InteropServices;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using ScreenCaptureApp.Windows.Clipboard;

namespace ScreenCaptureApp.Windows.Tests.Clipboard;

public sealed class ClipboardServiceTests
{
    [Fact]
    public async Task SetImageAsync_RetriesBoundedContentionThenSucceeds()
    {
        int calls = 0;
        ClipboardService service = new(
            Dispatcher.CurrentDispatcher,
            maximumAttempts: 3,
            retryDelay: TimeSpan.Zero,
            _ =>
            {
                calls++;
                if (calls < 3)
                {
                    throw new ClipboardBusyException();
                }
            });

        ClipboardWriteResult result = await service.SetImageAsync(CreateImage());

        Assert.True(result.Succeeded);
        Assert.Equal(3, result.Attempts);
        Assert.Equal(3, calls);
    }

    [Fact]
    public async Task SetImageAsync_ReturnsActionableFailureAtRetryLimit()
    {
        ClipboardService service = new(
            Dispatcher.CurrentDispatcher,
            maximumAttempts: 2,
            retryDelay: TimeSpan.Zero,
            _ => throw new ClipboardBusyException());

        ClipboardWriteResult result = await service.SetImageAsync(CreateImage());

        Assert.False(result.Succeeded);
        Assert.Equal(2, result.Attempts);
        Assert.Contains("busy", result.Error, StringComparison.OrdinalIgnoreCase);
    }

    private static BitmapSource CreateImage() => BitmapSource.Create(
        1,
        1,
        96,
        96,
        PixelFormats.Bgra32,
        palette: null,
        pixels: new byte[] { 0, 0, 0, 255 },
        stride: 4);

    private sealed class ClipboardBusyException : ExternalException;
}
