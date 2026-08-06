using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SnipSnap.App.Recognition;

internal interface ITranslationService
{
    Task<string> TranslateAsync(
        string text,
        Uri endpoint,
        string targetLanguage,
        string? apiKey = null,
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
        string? apiKey = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(text);
        ArgumentNullException.ThrowIfNull(endpoint);
        ArgumentException.ThrowIfNullOrWhiteSpace(targetLanguage);

        TranslationRequest request = new(
            text,
            "auto",
            targetLanguage,
            "text",
            string.IsNullOrWhiteSpace(apiKey) ? null : apiKey.Trim());

        using HttpResponseMessage response = await Client.PostAsJsonAsync(endpoint, request, cancellationToken)
            .ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            string? detail = await ReadErrorAsync(response, cancellationToken).ConfigureAwait(false);
            throw new InvalidOperationException(Describe(response.StatusCode, detail));
        }

        TranslationResponse? result = await response.Content
            .ReadFromJsonAsync<TranslationResponse>(cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        return string.IsNullOrWhiteSpace(result?.TranslatedText)
            ? throw new InvalidOperationException("The translation service returned no text.")
            : result.TranslatedText.Trim();
    }

    // LibreTranslate reports failures as {"error": "..."}; surface that instead of a bare status code.
    private static async Task<string?> ReadErrorAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        try
        {
            string body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(body)) return null;
            using JsonDocument document = JsonDocument.Parse(body);
            return document.RootElement.TryGetProperty("error", out JsonElement error)
                ? error.GetString()
                : null;
        }
        catch (Exception exception) when (exception is JsonException or HttpRequestException or OperationCanceledException)
        {
            // A malformed or truncated error body must not mask the real status code.
            return null;
        }
    }

    internal static string Describe(HttpStatusCode status, string? detail)
    {
        string suffix = string.IsNullOrWhiteSpace(detail) ? string.Empty : $" The service said: {detail.Trim()}";
        return status switch
        {
            HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden =>
                "The translation endpoint refused the request because it needs an API key. "
                + $"Add the key for this service in Settings, then try again.{suffix}",
            HttpStatusCode.TooManyRequests =>
                $"The translation endpoint is rate limiting this client.{suffix}",
            HttpStatusCode.NotFound =>
                "The translation endpoint returned 404. Check that the URL ends with /translate "
                + $"rather than pointing at the site root.{suffix}",
            _ => $"The translation endpoint returned {(int)status} ({status}).{suffix}"
        };
    }

    internal sealed record TranslationRequest(
        [property: JsonPropertyName("q")] string Text,
        [property: JsonPropertyName("source")] string Source,
        [property: JsonPropertyName("target")] string Target,
        [property: JsonPropertyName("format")] string Format,
        [property: JsonPropertyName("api_key")]
        [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? ApiKey);

    private sealed record TranslationResponse(
        [property: JsonPropertyName("translatedText")] string TranslatedText);
}
