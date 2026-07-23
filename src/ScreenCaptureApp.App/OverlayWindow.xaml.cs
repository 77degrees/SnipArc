using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Button = System.Windows.Controls.Button;
using Color = System.Windows.Media.Color;
using Cursors = System.Windows.Input.Cursors;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;
using MessageBox = System.Windows.MessageBox;
using Point = System.Windows.Point;
using Size = System.Windows.Size;

namespace ScreenCaptureApp.App;

public partial class OverlayWindow : Window
{
    private static readonly Color[] Colors = [System.Windows.Media.Colors.Red, System.Windows.Media.Colors.DodgerBlue, System.Windows.Media.Colors.LimeGreen, System.Windows.Media.Colors.Gold, System.Windows.Media.Colors.Black, System.Windows.Media.Colors.White];
    private readonly CaptureSurface _surface;
    private readonly Func<BitmapSource, Task<bool>> _copyAsync;
    private readonly Func<BitmapSource, bool, bool, Task<bool>> _saveAsync;
    private readonly Func<BitmapSource, Task<bool>> _analyzeAsync;
    private readonly Action<Rect> _recordGif;
    private readonly Action<Rect> _scrollingCapture;
    private int _colorIndex;
    private bool _operationInProgress;

    internal OverlayWindow(BitmapSource source, Rect virtualBounds, IReadOnlyList<WindowSuggestion> windowSuggestions,
        Func<BitmapSource, Task<bool>> copyAsync, Func<BitmapSource, bool, bool, Task<bool>> saveAsync,
        Func<BitmapSource, Task<bool>> analyzeAsync, Action<Rect, Rect> recordGif,
        Action<Rect, Rect> scrollingCapture)
    {
        InitializeComponent();
        _copyAsync = copyAsync;
        _saveAsync = saveAsync;
        _analyzeAsync = analyzeAsync;
        _recordGif = selection => recordGif(selection, virtualBounds);
        _scrollingCapture = selection => scrollingCapture(selection, virtualBounds);
        _surface = new CaptureSurface(source, windowSuggestions);
        _surface.StateChanged += Surface_StateChanged;
        _surface.TextRequested += Surface_TextRequested;
        SurfaceHost.Content = _surface;
        Left = virtualBounds.Left;
        Top = virtualBounds.Top;
        Width = virtualBounds.Width;
        Height = virtualBounds.Height;
        Loaded += Window_Loaded;
        SizeChanged += (_, _) => PositionToolbars();
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        // The native position call ensures the overlay covers negative-coordinate monitors too.
        var handle = new WindowInteropHelper(this).Handle;
        NativeMethods.SetWindowPos(handle, NativeMethods.HwndTopMost, (int)Left, (int)Top, (int)Width, (int)Height,
            NativeMethods.SwpShowWindow | NativeMethods.SwpNoActivate);
        Activate();
        _surface.Focus();
        _surface.RefreshWindowSuggestion(Mouse.GetPosition(_surface));
        PositionToolbars();
    }

    private void Tool_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: string name } || !Enum.TryParse<EditorTool>(name, out var tool)) return;
        SetTool(tool);
        _surface.Focus();
    }

    private void SetTool(EditorTool tool)
    {
        _surface.Tool = tool;
        _surface.Cursor = tool == EditorTool.Select ? Cursors.SizeAll : Cursors.Cross;
        foreach (var button in ToolStack.Children.OfType<Button>())
        {
            button.Background = button.Tag is string name &&
                                Enum.TryParse<EditorTool>(name, out var candidate) &&
                                candidate == tool
                ? (System.Windows.Media.Brush)FindResource("ToolbarAccentBrush")
                : System.Windows.Media.Brushes.Transparent;
        }
    }

    private void Color_Click(object sender, RoutedEventArgs e)
    {
        _colorIndex = (_colorIndex + 1) % Colors.Length;
        _surface.AnnotationColor = Colors[_colorIndex];
        ColorSwatch.Fill = new SolidColorBrush(Colors[_colorIndex]);
        _surface.Focus();
    }

    private void WidthSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_surface is not null) _surface.StrokeWidth = e.NewValue;
    }

    private void Undo_Click(object sender, RoutedEventArgs e) => _surface.Undo();
    private void Redo_Click(object sender, RoutedEventArgs e) => _surface.Redo();
    private async void Copy_Click(object sender, RoutedEventArgs e) => await CompleteAsync(_copyAsync);
    private async void Save_Click(object sender, RoutedEventArgs e) => await CompleteAsync(image => _saveAsync(image, false, _surface.HasOpaqueRedactions));
    private async void SaveAs_Click(object sender, RoutedEventArgs e) => await CompleteAsync(image => _saveAsync(image, true, _surface.HasOpaqueRedactions));
    private async void Analyze_Click(object sender, RoutedEventArgs e) => await CompleteAsync(_analyzeAsync);
    private void RecordGif_Click(object sender, RoutedEventArgs e)
    {
        if (!_surface.HasSelection || _operationInProgress) return;
        _recordGif(_surface.Selection);
        Close();
    }
    private void ScrollingCapture_Click(object sender, RoutedEventArgs e)
    {
        if (!_surface.HasSelection || _operationInProgress) return;
        _scrollingCapture(_surface.Selection);
        Close();
    }
    private void Cancel_Click(object sender, RoutedEventArgs e) => Close();

    private async Task CompleteAsync(Func<BitmapSource, Task<bool>> action)
    {
        if (_operationInProgress || !_surface.HasSelection) return;
        try
        {
            _operationInProgress = true;
            ActionPanel.IsEnabled = false;
            var image = _surface.RenderSelection();
            if (await action(image)) Close();
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "SnipArc", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            _operationInProgress = false;
            ActionPanel.IsEnabled = true;
            _surface.Focus();
        }
    }

    private void Surface_StateChanged(object? sender, EventArgs e)
    {
        UndoButton.IsEnabled = _surface.CanUndo;
        RedoButton.IsEnabled = _surface.CanRedo;
        RecordGifButton.IsEnabled = !_surface.IsSelectionLocked;
        ScrollingCaptureButton.IsEnabled = !_surface.IsSelectionLocked;
        ToolPanel.Visibility = ActionPanel.Visibility = _surface.HasSelection ? Visibility.Visible : Visibility.Hidden;
        PositionToolbars();
    }

    private void Surface_TextRequested(object? sender, Point point)
    {
        var dialog = new TextInputWindow { Owner = this };
        if (dialog.ShowDialog() == true) _surface.AddText(point, dialog.EnteredText);
        _surface.Focus();
    }

    private void PositionToolbars()
    {
        if (!_surface.HasSelection || ActualWidth <= 0 || ActualHeight <= 0) return;
        ActionPanel.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        ToolPanel.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        var selection = _surface.Selection;
        var left = selection.Left * ActualWidth / _surface.Source.PixelWidth;
        var right = selection.Right * ActualWidth / _surface.Source.PixelWidth;
        var top = selection.Top * ActualHeight / _surface.Source.PixelHeight;
        var bottom = selection.Bottom * ActualHeight / _surface.Source.PixelHeight;
        const double gap = 8;
        var actionX = Math.Clamp(right - ActionPanel.DesiredSize.Width, gap, Math.Max(gap, ActualWidth - ActionPanel.DesiredSize.Width - gap));
        var actionY = bottom + ActionPanel.DesiredSize.Height + gap <= ActualHeight
            ? bottom + gap
            : Math.Max(gap, top - ActionPanel.DesiredSize.Height - gap);
        var toolX = right + ToolPanel.DesiredSize.Width + gap <= ActualWidth
            ? right + gap
            : Math.Max(gap, left - ToolPanel.DesiredSize.Width - gap);
        var toolY = Math.Clamp(bottom - ToolPanel.DesiredSize.Height, gap, Math.Max(gap, ActualHeight - ToolPanel.DesiredSize.Height - gap));
        Canvas.SetLeft(ActionPanel, actionX);
        Canvas.SetTop(ActionPanel, actionY);
        Canvas.SetLeft(ToolPanel, toolX);
        Canvas.SetTop(ToolPanel, toolY);
    }

    private async void Window_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape) { Close(); e.Handled = true; return; }
        var modifiers = Keyboard.Modifiers;
        if (modifiers.HasFlag(ModifierKeys.Control))
        {
            if (e.Key == Key.C) { await CompleteAsync(_copyAsync); e.Handled = true; }
            else if (e.Key == Key.S) { await CompleteAsync(image => _saveAsync(image, modifiers.HasFlag(ModifierKeys.Shift), _surface.HasOpaqueRedactions)); e.Handled = true; }
            else if (e.Key == Key.Z) { _surface.Undo(); e.Handled = true; }
            else if (e.Key == Key.Y) { _surface.Redo(); e.Handled = true; }
            else if (e.Key == Key.A) { _surface.SelectAll(); e.Handled = true; }
            return;
        }

        var amount = modifiers.HasFlag(ModifierKeys.Shift) ? 10 : 1;
        if (e.Key == Key.Left) { _surface.NudgeSelection(-amount, 0); e.Handled = true; }
        else if (e.Key == Key.Right) { _surface.NudgeSelection(amount, 0); e.Handled = true; }
        else if (e.Key == Key.Up) { _surface.NudgeSelection(0, -amount); e.Handled = true; }
        else if (e.Key == Key.Down) { _surface.NudgeSelection(0, amount); e.Handled = true; }
        else if (TryToolShortcut(e.Key, out var tool)) { SetTool(tool); e.Handled = true; }
    }

    private static bool TryToolShortcut(Key key, out EditorTool tool)
    {
        tool = key switch
        {
            Key.V => EditorTool.Select,
            Key.P => EditorTool.Pen,
            Key.L => EditorTool.Line,
            Key.A => EditorTool.Arrow,
            Key.R => EditorTool.Rectangle,
            Key.H => EditorTool.Highlight,
            Key.T => EditorTool.Text,
            Key.B => EditorTool.Pixelate,
            Key.D => EditorTool.Redact,
            _ => EditorTool.Select
        };
        return key is Key.V or Key.P or Key.L or Key.A or Key.R or Key.H or Key.T or Key.B or Key.D;
    }

    private static class NativeMethods
    {
        internal static readonly nint HwndTopMost = new(-1);
        internal const uint SwpNoActivate = 0x0010;
        internal const uint SwpShowWindow = 0x0040;

        [System.Runtime.InteropServices.DllImport("user32.dll", SetLastError = true)]
        [return: System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.Bool)]
        internal static extern bool SetWindowPos(nint hwnd, nint insertAfter, int x, int y, int cx, int cy, uint flags);
    }
}
