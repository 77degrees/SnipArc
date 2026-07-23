namespace ScreenCaptureApp.Core.Scrolling;

public sealed record PixelFrame
{
    public PixelFrame(int width, int height, byte[] pixels)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(width, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(height, 1);
        ArgumentNullException.ThrowIfNull(pixels);
        if (pixels.Length != checked(width * height * 4))
        {
            throw new ArgumentException("Pixel data must contain tightly packed BGRA32 rows.", nameof(pixels));
        }

        Width = width;
        Height = height;
        Pixels = pixels;
    }

    public int Width { get; }
    public int Height { get; }
    public byte[] Pixels { get; }
}

public sealed record VerticalStitchResult(PixelFrame Image, IReadOnlyList<int> Overlaps);

public static class VerticalFrameStitcher
{
    public static VerticalStitchResult Stitch(
        IReadOnlyList<PixelFrame> frames,
        int minimumOverlap = 24,
        double maximumMeanChannelDifference = 18)
    {
        ArgumentNullException.ThrowIfNull(frames);
        if (frames.Count == 0) throw new ArgumentException("At least one frame is required.", nameof(frames));
        ArgumentOutOfRangeException.ThrowIfLessThan(minimumOverlap, 1);
        ArgumentOutOfRangeException.ThrowIfNegative(maximumMeanChannelDifference);

        int width = frames[0].Width;
        if (frames.Any(frame => frame.Width != width))
        {
            throw new ArgumentException("All frames must have the same width.", nameof(frames));
        }

        int[] overlaps = new int[Math.Max(0, frames.Count - 1)];
        long totalHeight = frames.Sum(static frame => (long)frame.Height);
        for (int index = 1; index < frames.Count; index++)
        {
            int overlap = FindOverlap(
                frames[index - 1],
                frames[index],
                minimumOverlap,
                maximumMeanChannelDifference);
            overlaps[index - 1] = overlap;
            totalHeight -= overlap;
        }

        if (totalHeight > int.MaxValue)
        {
            throw new InvalidOperationException("The stitched image would be too tall.");
        }

        int outputHeight = (int)totalHeight;
        byte[] output = new byte[checked(width * outputHeight * 4)];
        int outputRow = 0;
        for (int index = 0; index < frames.Count; index++)
        {
            PixelFrame frame = frames[index];
            int skippedRows = index == 0 ? 0 : overlaps[index - 1];
            int copiedRows = frame.Height - skippedRows;
            Buffer.BlockCopy(
                frame.Pixels,
                checked(skippedRows * width * 4),
                output,
                checked(outputRow * width * 4),
                checked(copiedRows * width * 4));
            outputRow += copiedRows;
        }

        return new VerticalStitchResult(
            new PixelFrame(width, outputHeight, output),
            overlaps);
    }

    private static int FindOverlap(
        PixelFrame previous,
        PixelFrame current,
        int minimumOverlap,
        double maximumMeanChannelDifference)
    {
        int maximumOverlap = Math.Min(previous.Height, current.Height) - 1;
        if (maximumOverlap < minimumOverlap) return 0;

        int bestOverlap = 0;
        double bestScore = double.MaxValue;
        for (int overlap = minimumOverlap; overlap <= maximumOverlap; overlap++)
        {
            double score = CompareOverlap(previous, current, overlap);
            if (score > maximumMeanChannelDifference) continue;
            if (score < bestScore - 0.05 || Math.Abs(score - bestScore) <= 0.05 && overlap > bestOverlap)
            {
                bestScore = score;
                bestOverlap = overlap;
            }
        }

        return bestOverlap;
    }

    private static double CompareOverlap(PixelFrame previous, PixelFrame current, int overlap)
    {
        int xStep = Math.Max(1, previous.Width / 64);
        int yStep = Math.Max(1, overlap / 24);
        long difference = 0;
        long channels = 0;
        int previousFirstRow = previous.Height - overlap;

        for (int y = 0; y < overlap; y += yStep)
        {
            int previousRow = checked((previousFirstRow + y) * previous.Width * 4);
            int currentRow = checked(y * current.Width * 4);
            for (int x = 0; x < previous.Width; x += xStep)
            {
                int previousPixel = previousRow + x * 4;
                int currentPixel = currentRow + x * 4;
                difference += Math.Abs(previous.Pixels[previousPixel] - current.Pixels[currentPixel]);
                difference += Math.Abs(previous.Pixels[previousPixel + 1] - current.Pixels[currentPixel + 1]);
                difference += Math.Abs(previous.Pixels[previousPixel + 2] - current.Pixels[currentPixel + 2]);
                channels += 3;
            }
        }

        return channels == 0 ? double.MaxValue : (double)difference / channels;
    }
}
