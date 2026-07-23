using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using ScreenCaptureApp.App.Scrolling;
using ScreenCaptureApp.Core.Geometry;
using ScreenCaptureApp.Core.Scrolling;
using MessageBox = System.Windows.MessageBox;

namespace ScreenCaptureApp.App;

public partial class ScrollingCaptureWindow : Window
{
    private const int MaximumPages = 30;
    private readonly ScrollingCaptureService _service;
    private readonly PhysicalRect _bounds;
    private readonly string _path;
    private readonly Action<ScrollingCaptureResult> _saved;
    private readonly List<PixelFrame> _pages = [];
    private bool _working;

    internal ScrollingCaptureWindow(
        ScrollingCaptureService service,
        PhysicalRect bounds,
        string path,
        Action<ScrollingCaptureResult> saved)
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
    }

    private async void Capture_Click(object sender, RoutedEventArgs e)
    {
        if (_working) return;
        try
        {
            _working = true;
            CaptureButton.IsEnabled = FinishButton.IsEnabled = false;
            StatusText.Text = "Capturing…";
            Opacity = 0;
            await Task.Delay(100);
            PixelFrame page = _service.CapturePage(_bounds);
            _pages.Add(page);
            Opacity = 1;
            StatusText.Text = _pages.Count == MaximumPages
                ? $"{MaximumPages} pages captured. The page limit was reached; finish and save."
                : _pages.Count == 1
                ? "1 page captured. Scroll the content, then capture the next page."
                : $"{_pages.Count} pages captured. Scroll again or finish and save.";
        }
        catch (Exception ex)
        {
            Opacity = 1;
            MessageBox.Show(this, ex.GetBaseException().Message, "Page capture failed",
                MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        finally
        {
            _working = false;
            CaptureButton.IsEnabled = _pages.Count < MaximumPages;
            FinishButton.IsEnabled = _pages.Count > 0;
        }
    }

    private async void Finish_Click(object sender, RoutedEventArgs e)
    {
        if (_working || _pages.Count == 0) return;
        try
        {
            _working = true;
            CaptureButton.IsEnabled = FinishButton.IsEnabled = false;
            StatusText.Text = "Finding overlap and encoding PNG…";
            ScrollingCaptureResult result = await ScrollingCaptureService.StitchAndSaveAsync(_pages, _path);
            _saved(result);
            Close();
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.GetBaseException().Message, "Scrolling capture failed",
                MessageBoxButton.OK, MessageBoxImage.Error);
            CaptureButton.IsEnabled = FinishButton.IsEnabled = true;
        }
        finally
        {
            _working = false;
        }
    }

    private const uint WdaExcludeFromCapture = 0x00000011;

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetWindowDisplayAffinity(nint windowHandle, uint affinity);
}
