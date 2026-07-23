using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ScreenCaptureApp.App;

internal static class OpaqueRedactionCompositor
{
    public static BitmapSource Apply(BitmapSource rendered, Int32Rect cropBounds, IEnumerable<AnnotationVisual> annotations)
    {
        ArgumentNullException.ThrowIfNull(rendered);
        ArgumentNullException.ThrowIfNull(annotations);

        var redactions = annotations.Where(static annotation => annotation.Tool == EditorTool.Redact).ToArray();
        if (redactions.Length == 0) return rendered;

        BitmapSource source = rendered.Format == PixelFormats.Bgra32
            ? rendered
            : new FormatConvertedBitmap(rendered, PixelFormats.Bgra32, null, 0);
        var stride = checked(source.PixelWidth * 4);
        var pixels = new byte[checked(stride * source.PixelHeight)];
        source.CopyPixels(pixels, stride, 0);

        foreach (var redaction in redactions)
        {
            var bounds = Normalize(redaction.Start, redaction.End);
            var left = Math.Clamp((int)Math.Floor(bounds.Left) - cropBounds.X, 0, source.PixelWidth);
            var top = Math.Clamp((int)Math.Floor(bounds.Top) - cropBounds.Y, 0, source.PixelHeight);
            var right = Math.Clamp((int)Math.Ceiling(bounds.Right) - cropBounds.X, left, source.PixelWidth);
            var bottom = Math.Clamp((int)Math.Ceiling(bounds.Bottom) - cropBounds.Y, top, source.PixelHeight);

            for (var y = top; y < bottom; y++)
            {
                var offset = checked((y * stride) + (left * 4));
                for (var x = left; x < right; x++, offset += 4)
                {
                    pixels[offset] = redaction.Color.B;
                    pixels[offset + 1] = redaction.Color.G;
                    pixels[offset + 2] = redaction.Color.R;
                    pixels[offset + 3] = byte.MaxValue;
                }
            }
        }

        var result = BitmapSource.Create(
            source.PixelWidth,
            source.PixelHeight,
            source.DpiX,
            source.DpiY,
            PixelFormats.Bgra32,
            null,
            pixels,
            stride);
        result.Freeze();
        return result;
    }

    private static Rect Normalize(System.Windows.Point first, System.Windows.Point second) => new(
        Math.Min(first.X, second.X),
        Math.Min(first.Y, second.Y),
        Math.Abs(second.X - first.X),
        Math.Abs(second.Y - first.Y));
}
