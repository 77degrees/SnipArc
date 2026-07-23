using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace ScreenCaptureApp.Windows.Clipboard;

public sealed record ClipboardWriteResult(bool Succeeded, int Attempts, string? Error);

public sealed class ClipboardService
{
    private readonly Dispatcher _dispatcher;
    private readonly int _maximumAttempts;
    private readonly TimeSpan _retryDelay;
    private readonly Action<BitmapSource> _setImage;

    public ClipboardService(
        Dispatcher dispatcher,
        int maximumAttempts = 5,
        TimeSpan? retryDelay = null)
        : this(dispatcher, maximumAttempts, retryDelay, image => System.Windows.Clipboard.SetImage(image))
    {
    }

    internal ClipboardService(
        Dispatcher dispatcher,
        int maximumAttempts,
        TimeSpan? retryDelay,
        Action<BitmapSource> setImage)
    {
        _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
        ArgumentOutOfRangeException.ThrowIfLessThan(maximumAttempts, 1);
        _maximumAttempts = maximumAttempts;
        _retryDelay = retryDelay ?? TimeSpan.FromMilliseconds(75);
        _setImage = setImage ?? throw new ArgumentNullException(nameof(setImage));
    }

    public async Task<ClipboardWriteResult> SetImageAsync(
        BitmapSource image,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(image);

        for (int attempt = 1; attempt <= _maximumAttempts; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                if (_dispatcher.CheckAccess())
                {
                    _setImage(image);
                }
                else
                {
                    await _dispatcher.InvokeAsync(() => _setImage(image), DispatcherPriority.Send, cancellationToken);
                }

                return new ClipboardWriteResult(true, attempt, null);
            }
            catch (Exception ex) when (ex is COMException or ExternalException)
            {
                if (attempt == _maximumAttempts)
                {
                    return new ClipboardWriteResult(false, attempt, "The clipboard is busy. Try Copy again.");
                }

                await Task.Delay(_retryDelay, cancellationToken).ConfigureAwait(false);
            }
        }

        throw new InvalidOperationException("Clipboard retry loop terminated unexpectedly.");
    }
}
