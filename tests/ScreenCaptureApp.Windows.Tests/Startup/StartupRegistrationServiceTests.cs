using ScreenCaptureApp.Windows.Startup;

namespace ScreenCaptureApp.Windows.Tests.Startup;

public sealed class StartupRegistrationServiceTests
{
    [Fact]
    public void FormatCommand_AlwaysQuotesAbsoluteExecutablePath()
    {
        string executable = Path.Combine(Path.GetPathRoot(Environment.SystemDirectory)!, "Program Files", "Capture App", "Capture.exe");

        string command = StartupRegistrationService.FormatCommand(executable);

        Assert.Equal($"\"{executable}\" --startup", command);
    }

    [Fact]
    public void FormatCommand_RejectsRelativePath()
    {
        Assert.Throws<ArgumentException>(() => StartupRegistrationService.FormatCommand("Capture.exe"));
    }

    [Fact]
    public void FormatCommand_RejectsEmbeddedQuote()
    {
        Assert.Throws<ArgumentException>(() => StartupRegistrationService.FormatCommand("C:\\Apps\\bad\\\"name.exe"));
    }
}
