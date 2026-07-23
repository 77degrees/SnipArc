using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Tesseract;
using ZXing;
using ZXing.Common;
using ZXing.Multi;

namespace ScreenCaptureApp.App.Recognition;

internal sealed record RecognizedBarcode(string Format, string Text);

internal sealed record ImageRecognitionResult(
    string Text,
    float TextConfidence,
    IReadOnlyList<RecognizedBarcode> Barcodes);

internal interface IImageRecognitionService
{
    Task<ImageRecognitionResult> RecognizeAsync(BitmapSource image, CancellationToken cancellationToken = default);
}

internal sealed class ImageRecognitionService : IImageRecognitionService
{
    private readonly string _languageDataPath;

    internal ImageRecognitionService(string? languageDataPath = null)
    {
        _languageDataPath = languageDataPath ?? Path.Combine(AppContext.BaseDirectory, "tessdata");
    }

    public Task<ImageRecognitionResult> RecognizeAsync(
        BitmapSource image,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(image);
        BitmapSource frozenImage = image.IsFrozen ? image : image.CloneCurrentValue();
        frozenImage.Freeze();

        return Task.Run(() => Recognize(frozenImage, cancellationToken), cancellationToken);
    }

    private ImageRecognitionResult Recognize(BitmapSource image, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        byte[] encodedImage = EncodePng(image);

        string text = string.Empty;
        float confidence = 0;
        if (File.Exists(Path.Combine(_languageDataPath, "eng.traineddata")))
        {
            using TesseractEngine engine = new(_languageDataPath, "eng", EngineMode.LstmOnly);
            using Pix pixels = Pix.LoadFromMemory(encodedImage);
            using Page page = engine.Process(pixels, PageSegMode.Auto);
            text = page.GetText()?.Trim() ?? string.Empty;
            confidence = page.GetMeanConfidence();
        }

        cancellationToken.ThrowIfCancellationRequested();
        IReadOnlyList<RecognizedBarcode> barcodes = DecodeBarcodes(image);
        return new ImageRecognitionResult(text, confidence, barcodes);
    }

    private static RecognizedBarcode[] DecodeBarcodes(BitmapSource image)
    {
        BitmapSource bgra = image.Format == PixelFormats.Bgra32
            ? image
            : new FormatConvertedBitmap(image, PixelFormats.Bgra32, null, 0);
        int stride = checked(bgra.PixelWidth * 4);
        byte[] pixels = new byte[checked(stride * bgra.PixelHeight)];
        bgra.CopyPixels(pixels, stride, 0);

        RGBLuminanceSource luminance = new(
            pixels,
            bgra.PixelWidth,
            bgra.PixelHeight,
            RGBLuminanceSource.BitmapFormat.BGRA32);
        BinaryBitmap bitmap = new(new HybridBinarizer(luminance));
        MultiFormatReader reader = new();
        reader.Hints = new Dictionary<DecodeHintType, object>
        {
            [DecodeHintType.TRY_HARDER] = true,
            [DecodeHintType.ALSO_INVERTED] = true
        };

        try
        {
            GenericMultipleBarcodeReader multipleReader = new(reader);
            Result[]? results = multipleReader.decodeMultiple(bitmap);
            return results?
                .Where(result => !string.IsNullOrWhiteSpace(result.Text))
                .Select(result => new RecognizedBarcode(result.BarcodeFormat.ToString(), result.Text))
                .Distinct()
                .ToArray() ?? [];
        }
        catch (ReaderException)
        {
            return [];
        }
    }

    private static byte[] EncodePng(BitmapSource image)
    {
        PngBitmapEncoder encoder = new();
        encoder.Frames.Add(BitmapFrame.Create(image));
        using MemoryStream stream = new();
        encoder.Save(stream);
        return stream.ToArray();
    }
}
