using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Security.Cryptography;
using System.Security.Principal;
using System.Text;

namespace ScreenCaptureApp.Windows.SingleInstance;

public sealed class SingleInstanceService : IAsyncDisposable, IDisposable
{
    private const byte ActivateCommand = 1;
    private readonly string _lockFilePath;
    private readonly string _pipeName;
    private readonly CancellationTokenSource _shutdown = new();
    private Task? _serverTask;
    private FileStream? _lockFile;
    private bool _disposed;

    public SingleInstanceService(string applicationId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(applicationId);
        string scope = CreateScope(applicationId);
        _pipeName = scope;
        _lockFilePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            SanitizeName(applicationId),
            $"instance.{scope}.lock");
    }

    public event EventHandler? ActivationRequested;

    public bool TryAcquire()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_lockFile is not null)
        {
            return true;
        }

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_lockFilePath)!);
            _lockFile = new FileStream(
                _lockFilePath,
                FileMode.OpenOrCreate,
                FileAccess.ReadWrite,
                FileShare.None,
                bufferSize: 1,
                FileOptions.WriteThrough);
        }
        catch (IOException)
        {
            return false;
        }

        _serverTask = RunServerAsync(_shutdown.Token);
        return true;
    }

    public async Task<bool> SendActivationAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_lockFile is not null)
        {
            return false;
        }

        for (int attempt = 0; attempt < 4; attempt++)
        {
            try
            {
                await using NamedPipeClientStream client = new(
                    serverName: ".",
                    _pipeName,
                    PipeDirection.Out,
                    PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
                await client.ConnectAsync(250, cancellationToken).ConfigureAwait(false);
                await client.WriteAsync(new[] { ActivateCommand }, cancellationToken).ConfigureAwait(false);
                await client.FlushAsync(cancellationToken).ConfigureAwait(false);
                return true;
            }
            catch (TimeoutException)
            {
                if (attempt == 3)
                {
                    return false;
                }

                await Task.Delay(50, cancellationToken).ConfigureAwait(false);
            }
            catch (IOException)
            {
                if (attempt == 3)
                {
                    return false;
                }

                await Task.Delay(50, cancellationToken).ConfigureAwait(false);
            }
        }

        return false;
    }

    private async Task RunServerAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await using NamedPipeServerStream server = new(
                    _pipeName,
                    PipeDirection.In,
                    maxNumberOfServerInstances: 1,
                    PipeTransmissionMode.Byte,
                    PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly,
                    inBufferSize: 1,
                    outBufferSize: 1);

                await server.WaitForConnectionAsync(cancellationToken).ConfigureAwait(false);
                byte[] command = new byte[1];
                int bytesRead = await server.ReadAsync(command, cancellationToken).ConfigureAwait(false);
                if (bytesRead == 1 && command[0] == ActivateCommand)
                {
                    ActivationRequested?.Invoke(this, EventArgs.Empty);
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return;
            }
            catch (IOException) when (!cancellationToken.IsCancellationRequested)
            {
                // A client can disconnect at any time. Recreate the bounded server endpoint.
            }
        }
    }

    private static string CreateScope(string applicationId)
    {
        string sid = WindowsIdentity.GetCurrent().User?.Value ?? Environment.UserName;
        string input = $"{applicationId}|{sid}|{Process.GetCurrentProcess().SessionId}";
        string hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(input)))[..24];
        return $"{SanitizeName(applicationId)}.{hash}";
    }

    private static string SanitizeName(string value)
    {
        char[] sanitized = value.Select(character => char.IsAsciiLetterOrDigit(character) ? character : '_').ToArray();
        return new string(sanitized);
    }

    public void Dispose()
    {
        DisposeAsync().AsTask().GetAwaiter().GetResult();
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        await _shutdown.CancelAsync().ConfigureAwait(false);
        if (_serverTask is not null)
        {
            try
            {
                await _serverTask.WaitAsync(TimeSpan.FromSeconds(2)).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is OperationCanceledException or TimeoutException)
            {
                // Expected during shutdown.
            }
        }

        _lockFile?.Dispose();
        _lockFile = null;
        _shutdown.Dispose();
    }
}
