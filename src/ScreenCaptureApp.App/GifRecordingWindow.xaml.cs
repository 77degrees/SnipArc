using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using ScreenCaptureApp.App.Recording;
using ScreenCaptureApp.Core.Geometry;
using MessageBox = System.Windows.MessageBox;

namespace ScreenCaptureApp.App;

public partial class GifRecordingWindow : Window, IDisposable
{
    private readonly GifRecordingService _service;
    private readonly PhysicalRect _bounds;
    private readonly string _path;
    private readonly Action<string, int> _saved;
    private readonly CancellationTokenSource _stop = new();
    private bool _discard;
    private bool _recording;
    private bool _disposed;

    internal GifRecordingWindow(
        GifRecordingService service,
        PhysicalRect bounds,
        string path,
        Action<string, int> saved)
    {
        InitializeComponent();
        _service = service;
        _bounds = bounds;
        _path = path;
        _saved = saved;
        SourceInitialized += (_, _) =>
        {
            nint handle = new WindowInteropHelper(this).Handle;
            _ = SetWindowDisplayAffinity(handle, WdaExcludeFromCapture);
        };
        Loaded += Window_Loaded;
        Closing += (_, _) =>
        {
            if (_recording) _stop.Cancel();
        };
    }

    private async void Window_Loaded(object sender, RoutedEventArgs e)
    {
        try
        {
            _recording = true;
            Progress<TimeSpan> progress = new(elapsed =>
                StatusText.Text = $"{elapsed:mm\\:ss} • maximum 00:15");
            int frameCount = await _service.RecordToFileAsync(
                _bounds,
                _path,
                new GifRecordingOptions(TimeSpan.FromMilliseconds(125), TimeSpan.FromSeconds(15)),
                progress,
                _stop.Token);
            _recording = false;

            if (_discard)
            {
                try { File.Delete(_path); } catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
            }
            else
            {
                _saved(_path, frameCount);
            }
            Close();
        }
        catch (Exception ex)
        {
            _recording = false;
            MessageBox.Show(ex.GetBaseException().Message, "GIF recording failed",
                MessageBoxButton.OK, MessageBoxImage.Error);
            Close();
        }
    }

    private void Stop_Click(object sender, RoutedEventArgs e)
    {
        StatusText.Text = "Encoding…";
        _stop.Cancel();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        _discard = true;
        StatusText.Text = "Discarding…";
        _stop.Cancel();
    }

    private const uint WdaExcludeFromCapture = 0x00000011;

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _stop.Cancel();
        _stop.Dispose();
        GC.SuppressFinalize(this);
    }

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetWindowDisplayAffinity(nint windowHandle, uint affinity);
}
