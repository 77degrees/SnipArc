using System.IO;
using System.Text.Json;

namespace ScreenCaptureApp.Windows.Settings;

public sealed record SettingsLoadResult<T>(T Settings, bool RecoveredFromError, string? RecoveryReason);

public sealed class JsonSettingsStore<T> where T : class
{
    private readonly string _filePath;
    private readonly int _currentSchemaVersion;
    private readonly Func<T> _createDefaults;
    private readonly IReadOnlyDictionary<int, Func<JsonElement, T>> _migrations;
    private readonly JsonSerializerOptions _jsonOptions;

    public JsonSettingsStore(
        string applicationName,
        string fileName,
        int currentSchemaVersion,
        Func<T> createDefaults,
        IReadOnlyDictionary<int, Func<JsonElement, T>>? migrations = null,
        string? localAppDataRoot = null,
        JsonSerializerOptions? jsonOptions = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(applicationName);
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);
        ArgumentOutOfRangeException.ThrowIfLessThan(currentSchemaVersion, 1);
        ArgumentNullException.ThrowIfNull(createDefaults);

        string root = localAppDataRoot ?? Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        _filePath = Path.Combine(root, applicationName, fileName);
        _currentSchemaVersion = currentSchemaVersion;
        _createDefaults = createDefaults;
        _migrations = migrations ?? new Dictionary<int, Func<JsonElement, T>>();
        _jsonOptions = jsonOptions ?? new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true
        };
    }

    public string FilePath => _filePath;

    public async Task<SettingsLoadResult<T>> LoadAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(_filePath))
        {
            return new SettingsLoadResult<T>(_createDefaults(), false, null);
        }

        try
        {
            await using FileStream stream = new(
                _filePath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                bufferSize: 4096,
                FileOptions.Asynchronous | FileOptions.SequentialScan);
            using JsonDocument document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken)
                .ConfigureAwait(false);

            JsonElement root = document.RootElement;
            if (!TryGetProperty(root, "schemaVersion", out JsonElement schemaElement) ||
                !schemaElement.TryGetInt32(out int schemaVersion) ||
                !TryGetProperty(root, "settings", out JsonElement settingsElement))
            {
                throw new JsonException("The settings envelope is invalid.");
            }

            if (schemaVersion == _currentSchemaVersion)
            {
                T? settings = settingsElement.Deserialize<T>(_jsonOptions);
                return settings is null
                    ? throw new JsonException("The settings payload is null.")
                    : new SettingsLoadResult<T>(settings, false, null);
            }

            if (schemaVersion > _currentSchemaVersion)
            {
                return new SettingsLoadResult<T>(
                    _createDefaults(),
                    true,
                    $"Settings schema {schemaVersion} is newer than supported schema {_currentSchemaVersion}.");
            }

            if (!_migrations.TryGetValue(schemaVersion, out Func<JsonElement, T>? migrate))
            {
                return new SettingsLoadResult<T>(
                    _createDefaults(),
                    true,
                    $"No migration is available from settings schema {schemaVersion}.");
            }

            return new SettingsLoadResult<T>(migrate(settingsElement), false, null);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or NotSupportedException)
        {
            return new SettingsLoadResult<T>(_createDefaults(), true, ex.Message);
        }
    }

    public async Task SaveAsync(T settings, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(settings);

        string? directory = Path.GetDirectoryName(_filePath);
        if (string.IsNullOrEmpty(directory))
        {
            throw new InvalidOperationException("Settings path has no parent directory.");
        }

        Directory.CreateDirectory(directory);
        string temporaryPath = Path.Combine(directory, $".{Path.GetFileName(_filePath)}.{Guid.NewGuid():N}.tmp");

        try
        {
            await using (FileStream stream = new(
                temporaryPath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                bufferSize: 4096,
                FileOptions.Asynchronous | FileOptions.WriteThrough))
            {
                SettingsEnvelope<T> envelope = new(_currentSchemaVersion, settings);
                await JsonSerializer.SerializeAsync(stream, envelope, _jsonOptions, cancellationToken)
                    .ConfigureAwait(false);
                await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
                stream.Flush(flushToDisk: true);
            }

            File.Move(temporaryPath, _filePath, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }
    }

    private static bool TryGetProperty(JsonElement element, string propertyName, out JsonElement value)
    {
        if (element.TryGetProperty(propertyName, out value)) return true;

        foreach (JsonProperty property in element.EnumerateObject())
        {
            if (!string.Equals(property.Name, propertyName, StringComparison.OrdinalIgnoreCase)) continue;
            value = property.Value;
            return true;
        }

        value = default;
        return false;
    }

    private sealed record SettingsEnvelope<TSettings>(int SchemaVersion, TSettings Settings);
}
