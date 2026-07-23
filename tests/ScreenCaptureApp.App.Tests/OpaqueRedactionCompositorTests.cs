using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ScreenCaptureApp.App;

namespace ScreenCaptureApp.App.Tests;

public sealed class OpaqueRedactionCompositorTests
{
    [Fact]
    public void ApplyReplacesEveryCoveredPixelWithOpaqueColor()
    {
        const int width = 4;
        const int height = 4;
        const int stride = width * 4;
        var sourcePixels = CreateSourcePixels(width, height);
        var source = BitmapSource.Create(width, height, 96, 96, PixelFormats.Bgra32, null, sourcePixels, stride);
        var redaction = new AnnotationVisual(
            EditorTool.Redact,
            new Point(1.2, 1.2),
            new Point(2.1, 2.1),
            Colors.Red,
            1);

        var result = OpaqueRedactionCompositor.Apply(
            source,
            new Int32Rect(0, 0, width, height),
            [redaction]);

        var actual = new byte[sourcePixels.Length];
        result.CopyPixels(actual, stride, 0);
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var offset = (y * stride) + (x * 4);
                var isRedacted = (x is 1 or 2) && (y is 1 or 2);
                Assert.Equal(isRedacted ? (byte)0 : (byte)10, actual[offset]);
                Assert.Equal(isRedacted ? (byte)0 : (byte)20, actual[offset + 1]);
                Assert.Equal(isRedacted ? byte.MaxValue : (byte)30, actual[offset + 2]);
                Assert.Equal(byte.MaxValue, actual[offset + 3]);
            }
        }
    }

    private static byte[] CreateSourcePixels(int width, int height)
    {
        var pixels = new byte[width * height * 4];
        for (var offset = 0; offset < pixels.Length; offset += 4)
        {
            pixels[offset] = 10;
            pixels[offset + 1] = 20;
            pixels[offset + 2] = 30;
            pixels[offset + 3] = byte.MaxValue;
        }

        return pixels;
    }
}
