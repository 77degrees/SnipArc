using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ScreenCaptureApp.App.Recognition;
using ZXing;
using ZXing.Common;
using ZXing.Rendering;

namespace ScreenCaptureApp.App.Tests.Recognition;

public sealed class ImageRecognitionServiceTests
{
    [Fact]
    public async Task RecognizeAsyncDecodesQrCodeLocally()
    {
        const string expected = "https://sniparc.example/test";
        BarcodeWriterPixelData writer = new()
        {
            Format = BarcodeFormat.QR_CODE,
            Options = new EncodingOptions
            {
                Width = 320,
                Height = 320,
                Margin = 4
            }
        };
        PixelData pixels = writer.Write(expected);
        BitmapSource image = BitmapSource.Create(
            pixels.Width,
            pixels.Height,
            96,
            96,
            PixelFormats.Bgra32,
            null,
            pixels.Pixels,
            pixels.Width * 4);
        image.Freeze();

        ImageRecognitionService service = new(Path.Combine(AppContext.BaseDirectory, "tessdata"));
        ImageRecognitionResult result = await service.RecognizeAsync(image);

        RecognizedBarcode barcode = Assert.Single(result.Barcodes);
        Assert.Equal(BarcodeFormat.QR_CODE.ToString(), barcode.Format);
        Assert.Equal(expected, barcode.Text);
    }
}
