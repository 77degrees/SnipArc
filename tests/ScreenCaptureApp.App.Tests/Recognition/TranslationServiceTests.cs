using System.Net;
using System.Text.Json;
using ScreenCaptureApp.App.Recognition;

namespace ScreenCaptureApp.App.Tests.Recognition;

public sealed class TranslationServiceTests
{
    [Theory]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.Forbidden)]
    public void DescribeTellsTheUserAnApiKeyIsNeeded(HttpStatusCode status)
    {
        string message = TranslationService.Describe(status, detail: null);

        Assert.Contains("API key", message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Settings", message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void DescribeSurfacesTheServerErrorText()
    {
        string message = TranslationService.Describe(
            HttpStatusCode.Forbidden,
            detail: "Invalid API key");

        Assert.Contains("Invalid API key", message, StringComparison.Ordinal);
    }

    [Fact]
    public void DescribePointsAtTheMissingPathWhenTheEndpointIsNotFound()
    {
        string message = TranslationService.Describe(HttpStatusCode.NotFound, detail: null);

        Assert.Contains("/translate", message, StringComparison.Ordinal);
    }

    [Fact]
    public void DescribeFallsBackToTheStatusCode()
    {
        string message = TranslationService.Describe(HttpStatusCode.BadGateway, detail: null);

        Assert.Contains("502", message, StringComparison.Ordinal);
    }

    [Fact]
    public void RequestOmitsApiKeyWhenNoneIsConfigured()
    {
        var request = new TranslationService.TranslationRequest("hola", "auto", "en", "text", ApiKey: null);

        string json = JsonSerializer.Serialize(request);

        // A null key must not serialize; LibreTranslate rejects an empty api_key outright.
        Assert.DoesNotContain("api_key", json, StringComparison.Ordinal);
    }

    [Fact]
    public void RequestSendsApiKeyWhenConfigured()
    {
        var request = new TranslationService.TranslationRequest("hola", "auto", "en", "text", ApiKey: "secret-key");

        string json = JsonSerializer.Serialize(request);

        Assert.Contains("\"api_key\":\"secret-key\"", json, StringComparison.Ordinal);
    }
}
