using ScreenCaptureApp.Core.Scrolling;

namespace ScreenCaptureApp.Core.Tests.Scrolling;

public sealed class VerticalFrameStitcherTests
{
    [Fact]
    public void StitchReconstructsOverlappingVerticalSlices()
    {
        PixelFrame source = CreateDistinctRows(width: 16, height: 100);
        PixelFrame first = Slice(source, 0, 60);
        PixelFrame second = Slice(source, 40, 60);

        VerticalStitchResult result = VerticalFrameStitcher.Stitch(
            [first, second],
            minimumOverlap: 8,
            maximumMeanChannelDifference: 0);

        Assert.Equal([20], result.Overlaps);
        Assert.Equal(source.Width, result.Image.Width);
        Assert.Equal(source.Height, result.Image.Height);
        Assert.Equal(source.Pixels, result.Image.Pixels);
    }

    [Fact]
    public void StitchAppendsFramesWhenNoOverlapMatches()
    {
        PixelFrame first = CreateSolid(width: 8, height: 12, value: 10);
        PixelFrame second = CreateSolid(width: 8, height: 12, value: 240);

        VerticalStitchResult result = VerticalFrameStitcher.Stitch(
            [first, second],
            minimumOverlap: 4,
            maximumMeanChannelDifference: 1);

        Assert.Equal([0], result.Overlaps);
        Assert.Equal(24, result.Image.Height);
    }

    private static PixelFrame CreateDistinctRows(int width, int height)
    {
        byte[] pixels = new byte[width * height * 4];
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                int index = (y * width + x) * 4;
                pixels[index] = (byte)(y * 17 % 251);
                pixels[index + 1] = (byte)(y * 37 % 253);
                pixels[index + 2] = (byte)(y * 73 % 255);
                pixels[index + 3] = 255;
            }
        }
        return new PixelFrame(width, height, pixels);
    }

    private static PixelFrame CreateSolid(int width, int height, byte value)
    {
        byte[] pixels = new byte[width * height * 4];
        for (int index = 0; index < pixels.Length; index += 4)
        {
            pixels[index] = value;
            pixels[index + 1] = value;
            pixels[index + 2] = value;
            pixels[index + 3] = 255;
        }
        return new PixelFrame(width, height, pixels);
    }

    private static PixelFrame Slice(PixelFrame source, int top, int height)
    {
        byte[] pixels = new byte[source.Width * height * 4];
        Buffer.BlockCopy(
            source.Pixels,
            top * source.Width * 4,
            pixels,
            0,
            pixels.Length);
        return new PixelFrame(source.Width, height, pixels);
    }
}
