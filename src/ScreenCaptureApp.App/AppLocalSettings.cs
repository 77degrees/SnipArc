using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using ScreenCaptureApp.Windows.Settings;

namespace ScreenCaptureApp.App;

internal enum ImageFileFormat { Png, Jpeg, Bmp }

internal sealed record AppLocalSettings
{
    public int SchemaVersion { get; init; } = 1;
    public string CaptureFolder { get; init; } = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), "Screen Captures");
    public ImageFileFormat QuickSaveFormat { get; init; } = ImageFileFormat.Png;
    public int JpegQuality { get; init; } = 92;
    public bool StartWithWindows { get; init; }
    public bool ShowNotifications { get; init; } = true;
    public bool IncludeCursor { get; init; }
    public bool OverrideWindowsSnippingShortcut { get; init; }
    public string? LastOutputFolder { get; init; }
    public string Hotkey { get; init; } = "PrintScreen";
    public string? TranslationEndpoint { get; init; }
    public string TranslationTargetLanguage { get; init; } = "en";
}

internal sealed class AppSettingsRepository
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.General)
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };
    private readonly JsonSettingsStore<AppLocalSettings> _store;

    internal AppSettingsRepository(string? localAppDataRoot = null)
    {
        _store = new JsonSettingsStore<AppLocalSettings>(
            "ScreenCaptureApp",
            "settings.json",
            1,
            static () => new AppLocalSettings(),
            localAppDataRoot: localAppDataRoot,
            jsonOptions: Options);
    }

    public async Task<AppLocalSettings> LoadAsync() => (await _store.LoadAsync()).Settings;

    public Task SaveAsync(AppLocalSettings settings) => _store.SaveAsync(settings);
}
