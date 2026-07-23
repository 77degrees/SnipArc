using System.Windows;
using ScreenCaptureApp.Windows.SingleInstance;
using MessageBox = System.Windows.MessageBox;

namespace ScreenCaptureApp.App;

[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1001:Types that own disposable fields should be disposable", Justification = "WPF Application releases owned services in OnExit.")]
public partial class App : System.Windows.Application
{
    private SingleInstanceService? _singleInstance;
    private AppController? _controller;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        EnsureWindowsDirectoryEnvironment();
        try
        {
            _singleInstance = new SingleInstanceService("ScreenCaptureApp");
            if (!_singleInstance.TryAcquire())
            {
                _ = await _singleInstance.SendActivationAsync();
                Shutdown();
                return;
            }

            _controller = new AppController(Dispatcher);
            _singleInstance.ActivationRequested += (_, _) => Dispatcher.BeginInvoke(_controller.BeginCapture);
            await _controller.InitializeAsync();
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "SnipArc could not start", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown(1);
        }
    }

    private static void EnsureWindowsDirectoryEnvironment()
    {
        if (!string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("WINDIR"))) return;

        var systemRoot = Environment.GetEnvironmentVariable("SystemRoot");
        if (!string.IsNullOrWhiteSpace(systemRoot))
        {
            Environment.SetEnvironmentVariable("WINDIR", systemRoot, EnvironmentVariableTarget.Process);
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _controller?.Dispose();
        _singleInstance?.Dispose();
        base.OnExit(e);
    }
}
