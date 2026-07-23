using System.Windows;
using ScreenCaptureApp.App.Recognition;
using MessageBox = System.Windows.MessageBox;

namespace ScreenCaptureApp.App;

public partial class RecognitionWindow : Window
{
    private readonly ImageRecognitionResult _result;
    private readonly ITranslationService _translationService;
    private readonly Uri? _translationEndpoint;
    private readonly string _targetLanguage;

    internal RecognitionWindow(
        ImageRecognitionResult result,
        string? translationEndpoint,
        string targetLanguage,
        ITranslationService? translationService = null)
    {
        InitializeComponent();
        _result = result;
        _translationService = translationService ?? new TranslationService();
        _targetLanguage = targetLanguage;
        _translationEndpoint = Uri.TryCreate(translationEndpoint, UriKind.Absolute, out Uri? endpoint)
            ? endpoint
            : null;

        TextBox.Text = result.Text;
        BarcodeList.ItemsSource = result.Barcodes;
        SummaryText.Text = $"{result.TextConfidence:P0} OCR confidence • {result.Barcodes.Count} code(s) found";
        TranslateButton.IsEnabled = _translationEndpoint is not null && !string.IsNullOrWhiteSpace(result.Text);
    }

    private void Copy_Click(object sender, RoutedEventArgs e)
    {
        string content = string.Join(
            Environment.NewLine + Environment.NewLine,
            new[]
            {
                _result.Text,
                string.Join(Environment.NewLine, _result.Barcodes.Select(code => $"{code.Format}: {code.Text}"))
            }.Where(value => !string.IsNullOrWhiteSpace(value)));
        if (!string.IsNullOrWhiteSpace(content)) System.Windows.Clipboard.SetText(content);
    }

    private async void Translate_Click(object sender, RoutedEventArgs e)
    {
        if (_translationEndpoint is null || string.IsNullOrWhiteSpace(_result.Text)) return;

        try
        {
            TranslateButton.IsEnabled = false;
            TranslationBox.Text = "Translating…";
            TranslationBox.Text = await _translationService.TranslateAsync(
                _result.Text,
                _translationEndpoint,
                _targetLanguage);
        }
        catch (Exception ex)
        {
            TranslationBox.Clear();
            MessageBox.Show(this, ex.GetBaseException().Message, "Translation failed",
                MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        finally
        {
            TranslateButton.IsEnabled = true;
        }
    }
}
