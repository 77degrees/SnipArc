using System.IO;
using SnipSnap.Windows.SingleInstance;

namespace SnipSnap.Windows.Tests.SingleInstance;

public sealed class SingleInstanceServiceTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        $"SnipSnap.SingleInstance.Tests.{Guid.NewGuid():N}");

    [Fact]
    public async Task SecondInstance_CannotAcquire_AndCanRequestActivation()
    {
        string applicationId = $"SnipSnap.Test.{Guid.NewGuid():N}";
        await using SingleInstanceService primary = new(applicationId, _root);
        await using SingleInstanceService secondary = new(applicationId, _root);
        TaskCompletionSource activation = new(TaskCreationOptions.RunContinuationsAsynchronously);
        primary.ActivationRequested += (_, _) => activation.TrySetResult();

        Assert.True(primary.TryAcquire());
        Assert.False(secondary.TryAcquire());
        Assert.True(await secondary.SendActivationAsync());
        await activation.Task.WaitAsync(TimeSpan.FromSeconds(3));
    }

    [Fact]
    public void LockFile_StaysUnderTheSuppliedRoot()
    {
        string applicationId = $"SnipSnap.Test.{Guid.NewGuid():N}";
        using SingleInstanceService service = new(applicationId, _root);

        Assert.True(service.TryAcquire());
        Assert.True(Directory.Exists(_root));
        // Regression guard: before the root became injectable this landed in
        // %LOCALAPPDATA% and every test run left a directory behind for good.
        Assert.Empty(Directory.EnumerateDirectories(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            $"{SanitizedId(applicationId)}*"));
    }

    private static string SanitizedId(string value) =>
        new(value.Select(c => char.IsAsciiLetterOrDigit(c) ? c : '_').ToArray());

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }
}
