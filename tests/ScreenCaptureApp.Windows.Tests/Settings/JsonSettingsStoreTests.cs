using System.Text.Json;
using ScreenCaptureApp.Windows.Settings;

namespace ScreenCaptureApp.Windows.Tests.Settings;

public sealed class JsonSettingsStoreTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"ScreenCaptureApp.Tests.{Guid.NewGuid():N}");

    [Fact]
    public async Task SaveAndLoad_RoundTripsVersionedEnvelope()
    {
        JsonSettingsStore<TestSettings> store = CreateStore();

        await store.SaveAsync(new TestSettings("PrintScreen", true));
        SettingsLoadResult<TestSettings> result = await store.LoadAsync();

        Assert.Equal(new TestSettings("PrintScreen", true), result.Settings);
        Assert.False(result.RecoveredFromError);
        using JsonDocument document = JsonDocument.Parse(await File.ReadAllTextAsync(store.FilePath));
        Assert.Equal(2, document.RootElement.GetProperty("schemaVersion").GetInt32());
        Assert.Empty(Directory.EnumerateFiles(Path.GetDirectoryName(store.FilePath)!, "*.tmp"));
    }

    [Fact]
    public async Task Load_MalformedJson_ReturnsSafeDefaults()
    {
        JsonSettingsStore<TestSettings> store = CreateStore();
        Directory.CreateDirectory(Path.GetDirectoryName(store.FilePath)!);
        await File.WriteAllTextAsync(store.FilePath, "{ truncated");

        SettingsLoadResult<TestSettings> result = await store.LoadAsync();

        Assert.Equal(new TestSettings("Default", false), result.Settings);
        Assert.True(result.RecoveredFromError);
        Assert.NotNull(result.RecoveryReason);
    }

    [Fact]
    public async Task Load_PascalCaseEnvelopeFromEarlierBuild_PreservesSettings()
    {
        var pascalCaseOptions = new JsonSerializerOptions(JsonSerializerDefaults.General)
        {
            WriteIndented = true
        };
        JsonSettingsStore<TestSettings> store = CreateStore(jsonOptions: pascalCaseOptions);
        await store.SaveAsync(new TestSettings("CtrlShiftS", true));

        string serialized = await File.ReadAllTextAsync(store.FilePath);
        Assert.Contains("\"SchemaVersion\"", serialized, StringComparison.Ordinal);
        Assert.Contains("\"Settings\"", serialized, StringComparison.Ordinal);

        SettingsLoadResult<TestSettings> result = await store.LoadAsync();

        Assert.Equal(new TestSettings("CtrlShiftS", true), result.Settings);
        Assert.False(result.RecoveredFromError);
    }

    [Fact]
    public async Task Load_OlderSchema_UsesRegisteredMigration()
    {
        Dictionary<int, Func<JsonElement, TestSettings>> migrations = new()
        {
            [1] = element => new TestSettings(element.GetProperty("key").GetString()!, false)
        };
        JsonSettingsStore<TestSettings> store = CreateStore(migrations);
        Directory.CreateDirectory(Path.GetDirectoryName(store.FilePath)!);
        await File.WriteAllTextAsync(store.FilePath, """
            { "schemaVersion": 1, "settings": { "key": "Migrated" } }
            """);

        SettingsLoadResult<TestSettings> result = await store.LoadAsync();

        Assert.Equal(new TestSettings("Migrated", false), result.Settings);
        Assert.False(result.RecoveredFromError);
    }

    private JsonSettingsStore<TestSettings> CreateStore(
        IReadOnlyDictionary<int, Func<JsonElement, TestSettings>>? migrations = null,
        JsonSerializerOptions? jsonOptions = null) =>
        new("ScreenCaptureApp", "settings.json", 2, () => new TestSettings("Default", false), migrations, _root, jsonOptions);

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    public sealed record TestSettings(string Key, bool IncludeCursor);
}
