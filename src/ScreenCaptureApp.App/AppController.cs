using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Microsoft.Win32;
using ScreenCaptureApp.Core.Geometry;
using ScreenCaptureApp.Windows.Capture;
using ScreenCaptureApp.Windows.Clipboard;
using ScreenCaptureApp.Windows.Displays;
using ScreenCaptureApp.Windows.Hotkeys;
using ScreenCaptureApp.Windows.Startup;
using ScreenCaptureApp.Windows.Windowing;
using MessageBox = System.Windows.MessageBox;
using SaveFileDialog = Microsoft.Win32.SaveFileDialog;
using WinForms = System.Windows.Forms;

namespace ScreenCaptureApp.App;

internal sealed class AppController : IDisposable
{
    private const uint VkSnapshot = 0x2C;
    private readonly Dispatcher _dispatcher;
    private readonly AppSettingsRepository _settingsRepository = new();
    private readonly DisplayTopologyService _topology = new();
    private readonly GdiScreenCaptureBackend _captureBackend = new();
    private readonly WindowDetectionService _windowDetection = new();
    private readonly StartupRegistrationService _startup = new("ScreenCaptureApp");
    private readonly HwndSource _messageWindow;
    private readonly GlobalHotkeyService _hotkey;
    private readonly WindowsSnippingShortcutService _windowsSnippingShortcut = new();
    private readonly ClipboardService _clipboard;
    private readonly System.Drawing.Icon _appIcon;
    private readonly WinForms.NotifyIcon _trayIcon;
    private AppLocalSettings _settings = new();
    private OverlayWindow? _overlay;
    private bool _captureStarting;
    private bool _openSettingsFromBalloon;
    private bool _disposed;

    public AppController(Dispatcher dispatcher)
    {
        _dispatcher = dispatcher;
        var parameters = new HwndSourceParameters("ScreenCaptureApp.MessageWindow")
        {
            Width = 0,
            Height = 0,
            WindowStyle = unchecked((int)0x80000000)
        };
        _messageWindow = new HwndSource(parameters);
        _hotkey = new GlobalHotkeyService(_messageWindow);
        _hotkey.HotkeyPressed += (_, _) => _dispatcher.BeginInvoke(BeginCapture);
        _windowsSnippingShortcut.ShortcutPressed += (_, _) => _dispatcher.BeginInvoke(BeginCapture);
        _clipboard = new ClipboardService(dispatcher);
        _appIcon = LoadAppIcon();
        _trayIcon = CreateTrayIcon();
    }

    public async Task InitializeAsync()
    {
        _settings = await _settingsRepository.LoadAsync();
        _trayIcon.Visible = true;
        SystemEvents.DisplaySettingsChanged += OnDisplaySettingsChanged;
        ApplyStartupSetting(_settings.StartWithWindows);
        RegisterHotkey(showFailure: true);
        ApplyWindowsSnippingShortcutSetting(showFailure: true);
    }

    public void BeginCapture()
    {
        if (_disposed || _captureStarting || _overlay is { IsVisible: true }) return;
        try
        {
            _captureStarting = true;
            var topology = _topology.GetCurrent();
            var capture = _captureBackend.CaptureVirtualDesktop(topology, _settings.IncludeCursor);
            var cursor = WinForms.Cursor.Position;
            var display = topology.FindContaining(new PhysicalPoint(cursor.X, cursor.Y)) ?? topology.PrimaryDisplay;
            var cropBounds = new Int32Rect(
                display.Bounds.Left - capture.Bounds.Left,
                display.Bounds.Top - capture.Bounds.Top,
                display.Bounds.Width,
                display.Bounds.Height);
            var displayImage = new CroppedBitmap(capture.Image, cropBounds);
            displayImage.Freeze();
            var bounds = new Rect(display.Bounds.Left, display.Bounds.Top, display.Bounds.Width, display.Bounds.Height);
            var windowSuggestions = _windowDetection.GetVisibleWindows((uint)Environment.ProcessId)
                .Where(window => display.Bounds.Contains(window.Bounds))
                .Select(window => new WindowSuggestion(
                    window.Title,
                    new Rect(
                        window.Bounds.Left - display.Bounds.Left,
                        window.Bounds.Top - display.Bounds.Top,
                        window.Bounds.Width,
                        window.Bounds.Height)))
                .ToArray();
            _overlay = new OverlayWindow(displayImage, bounds, windowSuggestions, CopyAsync, SaveAsync);
            _overlay.Closed += (_, _) => _overlay = null;
            _overlay.Show();
        }
        catch (Exception ex)
        {
            ShowError("The screen could not be captured.", ex);
        }
        finally
        {
            _captureStarting = false;
        }
    }

    private WinForms.NotifyIcon CreateTrayIcon()
    {
        var menu = new WinForms.ContextMenuStrip();
        menu.Items.Add("Capture area", null, (_, _) => _dispatcher.BeginInvoke(BeginCapture));
        menu.Items.Add("Open capture folder", null, (_, _) => _dispatcher.BeginInvoke(OpenCaptureFolder));
        menu.Items.Add(new WinForms.ToolStripSeparator());
        menu.Items.Add("Settings…", null, (_, _) => _dispatcher.BeginInvoke(ShowSettings));
        menu.Items.Add("About", null, (_, _) => _dispatcher.BeginInvoke(ShowAbout));
        menu.Items.Add(new WinForms.ToolStripSeparator());
        menu.Items.Add("Exit", null, (_, _) => _dispatcher.BeginInvoke(Exit));
        var icon = new WinForms.NotifyIcon
        {
            Text = "SnipArc",
            Icon = _appIcon,
            ContextMenuStrip = menu
        };
        icon.DoubleClick += (_, _) => _dispatcher.BeginInvoke(BeginCapture);
        icon.BalloonTipClicked += (_, _) =>
        {
            if (_openSettingsFromBalloon) _dispatcher.BeginInvoke(ShowSettings);
        };
        return icon;
    }

    private async Task<bool> CopyAsync(BitmapSource image)
    {
        var result = await _clipboard.SetImageAsync(image);
        if (!result.Succeeded)
        {
            MessageBox.Show(_overlay, result.Error ?? "The clipboard could not be updated.", "Copy failed", MessageBoxButton.OK, MessageBoxImage.Warning);
            return false;
        }

        Notify("Screenshot copied", "The selected image is ready to paste.");
        return true;
    }

    private async Task<bool> SaveAsync(BitmapSource image, bool saveAs, bool hasOpaqueRedactions)
    {
        string? path;
        ImageFileFormat format;
        if (saveAs)
        {
            var dialog = new SaveFileDialog
            {
                Title = "Save screenshot",
                InitialDirectory = Directory.Exists(_settings.LastOutputFolder) ? _settings.LastOutputFolder :
                    Directory.Exists(_settings.CaptureFolder) ? _settings.CaptureFolder : Environment.GetFolderPath(Environment.SpecialFolder.MyPictures),
                FileName = CreateBaseFileName(),
                Filter = hasOpaqueRedactions
                    ? "PNG image (*.png)|*.png|Bitmap image (*.bmp)|*.bmp"
                    : "PNG image (*.png)|*.png|JPEG image (*.jpg)|*.jpg|Bitmap image (*.bmp)|*.bmp",
                AddExtension = true,
                OverwritePrompt = true,
                FilterIndex = hasOpaqueRedactions
                    ? (_settings.QuickSaveFormat == ImageFileFormat.Bmp ? 2 : 1)
                    : _settings.QuickSaveFormat switch { ImageFileFormat.Jpeg => 2, ImageFileFormat.Bmp => 3, _ => 1 }
            };
            if (dialog.ShowDialog(_overlay) != true) return false;
            path = dialog.FileName;
            format = FormatFromExtension(Path.GetExtension(path));
            if (hasOpaqueRedactions && format == ImageFileFormat.Jpeg)
            {
                MessageBox.Show(_overlay, "Captures containing opaque redaction must be saved as lossless PNG or BMP so neighboring pixels cannot bleed into the redacted area.",
                    "Choose a lossless format", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }
        }
        else
        {
            Directory.CreateDirectory(_settings.CaptureFolder);
            format = hasOpaqueRedactions && _settings.QuickSaveFormat == ImageFileFormat.Jpeg
                ? ImageFileFormat.Png
                : _settings.QuickSaveFormat;
            path = FindAvailablePath(_settings.CaptureFolder, CreateBaseFileName(), ExtensionFor(format));
        }

        await EncodeAtomicallyAsync(image, path, format, _settings.JpegQuality, allowOverwrite: saveAs);
        var outputFolder = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(outputFolder) && !string.Equals(outputFolder, _settings.LastOutputFolder, StringComparison.OrdinalIgnoreCase))
        {
            _settings = _settings with { LastOutputFolder = outputFolder };
            try { await _settingsRepository.SaveAsync(_settings); } catch (Exception) { }
        }
        Notify("Screenshot saved", path);
        return true;
    }

    private static async Task EncodeAtomicallyAsync(BitmapSource image, string path, ImageFileFormat format, int jpegQuality, bool allowOverwrite)
    {
        var directory = Path.GetDirectoryName(path);
        if (string.IsNullOrWhiteSpace(directory)) throw new IOException("The selected destination is not valid.");
        Directory.CreateDirectory(directory);
        var temporary = Path.Combine(directory, $".{Path.GetFileName(path)}.{Guid.NewGuid():N}.partial");
        BitmapEncoder encoder = format switch
        {
            ImageFileFormat.Jpeg => new JpegBitmapEncoder { QualityLevel = Math.Clamp(jpegQuality, 1, 100) },
            ImageFileFormat.Bmp => new BmpBitmapEncoder(),
            _ => new PngBitmapEncoder()
        };
        encoder.Frames.Add(BitmapFrame.Create(image));
        try
        {
            await using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, FileOptions.Asynchronous | FileOptions.WriteThrough))
            {
                encoder.Save(stream);
                await stream.FlushAsync();
            }
            File.Move(temporary, path, allowOverwrite);
        }
        catch
        {
            try { File.Delete(temporary); } catch (Exception cleanupError) when (cleanupError is IOException or UnauthorizedAccessException) { }
            throw;
        }
    }

    private async void ShowSettings()
    {
        var window = new SettingsWindow(_settings);
        if (window.ShowDialog() != true) return;
        var prior = _settings;
        var candidate = window.Settings;
        var hotkeyChanged = !string.Equals(prior.Hotkey, candidate.Hotkey, StringComparison.Ordinal);
        var snippingOverrideChanged = prior.OverrideWindowsSnippingShortcut != candidate.OverrideWindowsSnippingShortcut;
        if (hotkeyChanged && !_hotkey.TryRegister(ToGesture(candidate.Hotkey), out var hotkeyError))
        {
            MessageBox.Show(window, hotkeyError ?? "That capture shortcut is not available. Your previous shortcut is still active.",
                "Capture shortcut unavailable", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (snippingOverrideChanged && candidate.OverrideWindowsSnippingShortcut &&
            !_windowsSnippingShortcut.TryEnable(out var snippingError))
        {
            if (hotkeyChanged) _ = _hotkey.TryRegister(ToGesture(prior.Hotkey), out _);
            MessageBox.Show(window, snippingError ?? "Windows + Shift + S could not be assigned. Your previous settings are still active.",
                "Windows shortcut unavailable", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (snippingOverrideChanged && !candidate.OverrideWindowsSnippingShortcut) _windowsSnippingShortcut.Disable();

        try
        {
            ApplyStartupSetting(candidate.StartWithWindows);
            await _settingsRepository.SaveAsync(candidate);
            _settings = candidate;
        }
        catch (Exception ex)
        {
            if (hotkeyChanged) _ = _hotkey.TryRegister(ToGesture(prior.Hotkey), out _);
            if (snippingOverrideChanged)
            {
                if (prior.OverrideWindowsSnippingShortcut) _ = _windowsSnippingShortcut.TryEnable(out _);
                else _windowsSnippingShortcut.Disable();
            }
            try { ApplyStartupSetting(prior.StartWithWindows); } catch (Exception) { }
            _settings = prior;
            ShowError("Settings could not be saved.", ex);
        }
    }

    private void ApplyWindowsSnippingShortcutSetting(bool showFailure)
    {
        if (!_settings.OverrideWindowsSnippingShortcut || _windowsSnippingShortcut.TryEnable(out var error)) return;

        if (showFailure)
        {
            _openSettingsFromBalloon = true;
            _trayIcon.ShowBalloonTip(7000, "Windows shortcut unavailable",
                (error ?? "Windows + Shift + S could not be assigned.") + " Open Settings to disable the override or try again.",
                WinForms.ToolTipIcon.Warning);
        }
    }

    private void RegisterHotkey(bool showFailure)
    {
        if (_hotkey.TryRegister(ToGesture(_settings.Hotkey), out var error))
        {
            _openSettingsFromBalloon = false;
            return;
        }

        if (showFailure)
        {
            _openSettingsFromBalloon = true;
            _trayIcon.ShowBalloonTip(7000, "Capture shortcut unavailable", (error ?? "Choose a different shortcut in Settings.") + " You can still capture from the tray icon.", WinForms.ToolTipIcon.Warning);
        }
    }

    private static HotkeyGesture ToGesture(string hotkey) => hotkey switch
    {
        "CtrlShift4" => new HotkeyGesture(0x34, HotkeyModifiers.Control | HotkeyModifiers.Shift),
        "CtrlShiftS" => new HotkeyGesture(0x53, HotkeyModifiers.Control | HotkeyModifiers.Shift),
        _ => new HotkeyGesture(VkSnapshot)
    };

    private void ApplyStartupSetting(bool enabled)
    {
        var executable = Environment.ProcessPath ?? throw new InvalidOperationException("The application executable path is unavailable.");
        if (enabled) _startup.Enable(executable);
        else _startup.Disable();
    }

    private void OpenCaptureFolder()
    {
        try
        {
            var folder = Directory.Exists(_settings.LastOutputFolder) ? _settings.LastOutputFolder : _settings.CaptureFolder;
            Directory.CreateDirectory(folder);
            Process.Start(new ProcessStartInfo(folder) { UseShellExecute = true });
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.ComponentModel.Win32Exception)
        {
            ShowError("The capture folder could not be opened.", ex);
        }
    }

    private static void ShowAbout() => MessageBox.Show(
        "SnipArc 0.1 alpha\n\nA fast, local-only screenshot and annotation tool. No uploads, accounts, telemetry, or hidden screenshot history.",
        "About SnipArc", MessageBoxButton.OK, MessageBoxImage.Information);

    private void Notify(string title, string message)
    {
        if (_settings.ShowNotifications) _trayIcon.ShowBalloonTip(3500, title, message, WinForms.ToolTipIcon.Info);
    }

    private void ShowError(string message, Exception ex)
    {
        var details = $"{message}\n\n{ex.GetBaseException().Message}";
        if (_overlay is { IsVisible: true })
        {
            MessageBox.Show(_overlay, details, "SnipArc", MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        MessageBox.Show(details, "SnipArc", MessageBoxButton.OK, MessageBoxImage.Error);
    }

    private static string CreateBaseFileName() => $"Screenshot {DateTime.Now:yyyy-MM-dd HH-mm-ss}";
    private static string ExtensionFor(ImageFileFormat format) => format switch { ImageFileFormat.Jpeg => ".jpg", ImageFileFormat.Bmp => ".bmp", _ => ".png" };
    private static ImageFileFormat FormatFromExtension(string extension) => extension.ToLowerInvariant() switch { ".jpg" or ".jpeg" => ImageFileFormat.Jpeg, ".bmp" => ImageFileFormat.Bmp, _ => ImageFileFormat.Png };

    private static string FindAvailablePath(string directory, string name, string extension)
    {
        var path = Path.Combine(directory, name + extension);
        for (var index = 2; File.Exists(path); index++) path = Path.Combine(directory, $"{name} ({index}){extension}");
        return path;
    }

    private static void Exit() => System.Windows.Application.Current.Shutdown();

    private void OnDisplaySettingsChanged(object? sender, EventArgs e) =>
        _dispatcher.BeginInvoke(() => _overlay?.Close());

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        SystemEvents.DisplaySettingsChanged -= OnDisplaySettingsChanged;
        _overlay?.Close();
        _windowsSnippingShortcut.Dispose();
        _hotkey.Dispose();
        _trayIcon.Visible = false;
        _trayIcon.Dispose();
        _appIcon.Dispose();
        _messageWindow.Dispose();
    }

    private static System.Drawing.Icon LoadAppIcon()
    {
        var executablePath = Environment.ProcessPath;
        return executablePath is not null
            ? System.Drawing.Icon.ExtractAssociatedIcon(executablePath) ?? (System.Drawing.Icon)System.Drawing.SystemIcons.Application.Clone()
            : (System.Drawing.Icon)System.Drawing.SystemIcons.Application.Clone();
    }
}
