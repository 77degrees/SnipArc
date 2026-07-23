using ScreenCaptureApp.Windows.SingleInstance;

namespace ScreenCaptureApp.Windows.Tests.SingleInstance;

public sealed class SingleInstanceServiceTests
{
    [Fact]
    public async Task SecondInstance_CannotAcquire_AndCanRequestActivation()
    {
        string applicationId = $"ScreenCaptureApp.Test.{Guid.NewGuid():N}";
        await using SingleInstanceService primary = new(applicationId);
        await using SingleInstanceService secondary = new(applicationId);
        TaskCompletionSource activation = new(TaskCreationOptions.RunContinuationsAsynchronously);
        primary.ActivationRequested += (_, _) => activation.TrySetResult();

        Assert.True(primary.TryAcquire());
        Assert.False(secondary.TryAcquire());
        Assert.True(await secondary.SendActivationAsync());
        await activation.Task.WaitAsync(TimeSpan.FromSeconds(3));
    }
}
