using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace ScreenCaptureApp.App.Recognition;

internal interface ITranslationService
{
    Task<string> TranslateAsync(
        string text,
        Uri endpoint,
        string targetLanguage,
        CancellationToken cancellationToken = default);
}

internal sealed class TranslationService : ITranslationService
{
    private static readonly HttpClient Client = new()
    {
        Timeout = TimeSpan.FromSeconds(30)
    };

    public async Task<string> TranslateAsync(
        string text,
        Uri endpoint,
        string targetLanguage,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(text);
        ArgumentNullException.ThrowIfNull(endpoint);
        ArgumentException.ThrowIfNullOrWhiteSpace(targetLanguage);

        TranslationRequest request = new(text, "auto", targetLanguage, "text");
        using HttpResponseMessage response = await Client.PostAsJsonAsync(endpoint, request, cancellationToken)
            .ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        TranslationResponse? result = await response.Content
            .ReadFromJsonAsync<TranslationResponse>(cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        return string.IsNullOrWhiteSpace(result?.TranslatedText)
            ? throw new InvalidOperationException("The translation service returned no text.")
            : result.TranslatedText.Trim();
    }

    private sealed record TranslationRequest(
        [property: JsonPropertyName("q")] string Text,
        [property: JsonPropertyName("source")] string Source,
        [property: JsonPropertyName("target")] string Target,
        [property: JsonPropertyName("format")] string Format);

    private sealed record TranslationResponse(
        [property: JsonPropertyName("translatedText")] string TranslatedText);
}
