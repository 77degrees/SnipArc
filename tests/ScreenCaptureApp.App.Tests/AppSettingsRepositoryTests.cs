using System.IO;

namespace ScreenCaptureApp.App.Tests;

public sealed class AppSettingsRepositoryTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"SnipArc.AppSettings.Tests.{Guid.NewGuid():N}");

    [Fact]
    public async Task ReopenRepositoryPreservesEveryUserSetting()
    {
        var expected = new AppLocalSettings
        {
            CaptureFolder = @"C:\Captures",
            QuickSaveFormat = ImageFileFormat.Jpeg,
            JpegQuality = 100,
            StartWithWindows = true,
            ShowNotifications = false,
            IncludeCursor = true,
            OverrideWindowsSnippingShortcut = true,
            LastOutputFolder = @"C:\Captures\Recent",
            Hotkey = "CtrlShiftS"
        };

        await new AppSettingsRepository(_root).SaveAsync(expected);
        AppLocalSettings actual = await new AppSettingsRepository(_root).LoadAsync();

        Assert.Equal(expected, actual);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }
}
