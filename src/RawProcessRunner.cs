using System.Collections.ObjectModel;
using System.Diagnostics;

namespace Sashimi;

/// <summary>
/// Runs an external process and exposes its stdin/stdout/stderr as asynchronous streams.
/// Designed for PowerShell cmdlets that require non-blocking, cancellable process I/O.
/// </summary>
public sealed class RawProcessRunner : IAsyncDisposable
{
    /// <summary>
    /// Creates a <see cref="ProcessStartInfo"/> configured for raw I/O:
    /// shell disabled, and stdin/stdout/stderr redirected.
    /// </summary>
    public static ProcessStartInfo CreateProcessStartInfo(string fileName, IEnumerable<string> arguments)
        => new(fileName, arguments)
        {
            UseShellExecute = false,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };

    /// <summary>
    /// Initializes a new process runner for the specified executable and arguments.
    /// The process is not started until <see cref="StartAsync"/> is called.
    /// </summary>
    public RawProcessRunner(string fileName, IEnumerable<string> arguments)
    {
        var psi = CreateProcessStartInfo(fileName, arguments);
        _process = new() { StartInfo = psi };
        Arguments = psi.ArgumentList.AsReadOnly();
    }

    private readonly Process _process;
    private const int BufferSize = 4096;
    private Task _outputTask = null!;
    private CancellationTokenRegistration? _killRegistration;

    /// <summary>
    /// Gets the executable file name of the process.
    /// </summary>
    public string Name => _process.StartInfo.FileName;

    /// <summary>
    /// Gets the process ID after the process has started.
    /// Throws <see cref="InvalidOperationException"/> if accessed before <see cref="StartAsync"/>.
    /// </summary>
    public int Pid
    {
        get => field == -1
               ? throw new InvalidOperationException("Process has not been started yet.")
               : field;
        private set;
    } = -1;

    /// <inheritdoc cref="Process.StartTime"/>
    public DateTime StartTime { get; private set; }

    /// <inheritdoc cref="Process.ExitTime"/>
    public DateTime ExitTime { get; private set; }

    /// <summary>
    /// Gets the read-only list of arguments passed to the process.
    /// </summary>
    public ReadOnlyCollection<string> Arguments { get; }

    /// <summary>
    /// Raised whenever a chunk of stdout data is read.
    /// The byte array contains exactly the bytes read for that event.
    /// </summary>
    public event Action<byte[]>? OnStdout;

    /// <summary>
    /// Raised whenever a chunk of stderr data is read.
    /// </summary>
    public event Action<byte[]>? OnStderr;

    /// <summary>
    /// Starts the process and begins asynchronous reading of stdout and stderr.
    /// </summary>
    /// <param name="cancellationToken">
    /// Cancels process I/O and startup operations.  
    /// Typically passed from PowerShell's <c>PipelineStopToken</c>
    /// so that Ctrl+C or pipeline cancellation immediately stops all I/O.
    /// </param>
    public void Start(CancellationToken cancellationToken = default)
    {
        _process.Start();
        StartTime = _process.StartTime;

        // Ensure the process terminates properly upon cancellation
        _killRegistration = cancellationToken.Register(() => Kill());

        _outputTask = Task.WhenAll(ReadStdoutLoop(cancellationToken),
                                   ReadStderrLoop(cancellationToken));
        Pid = _process.Id;
    }

    /// <summary>
    /// Writes raw bytes to the process's standard input stream.
    /// </summary>
    public Task WriteStdinAsync(byte[] buffer, CancellationToken cancellationToken = default)
    {
        return _process.StandardInput.BaseStream.WriteAsync(buffer, 0, buffer.Length, cancellationToken);
    }

    /// <summary>
    /// Blocks until both stdout and stderr reading tasks complete.
    /// </summary>
    /// <param name="cancellationToken">
    /// Cancels the wait operation.  
    /// Does not kill the process; use <see cref="Kill"/> for that.
    /// </param>
    public Task WaitOutputAsync(CancellationToken cancellationToken = default)
        => _outputTask.WaitAsync(cancellationToken);

    /// <summary>
    /// Closes the process's standard input stream, signaling end-of-input.
    /// </summary>
    public void CloseStdin()
    {
        _process.StandardInput.Close();
    }

    /// <summary>
    /// Continuously reads stdout in chunks until the stream ends or cancellation is requested.
    /// Invokes <see cref="OnStdout"/> for each chunk.
    /// </summary>
    private async Task ReadStdoutLoop(CancellationToken cancellationToken = default)
    {
        var stream = _process.StandardOutput.BaseStream;
        var buffer = new byte[BufferSize];

        try
        {
            int read;
            while ((read = await stream.ReadAsync(buffer, cancellationToken)) > 0)
            {
                OnStdout?.Invoke(buffer.AsSpan(0, read).ToArray());
            }
        }
        catch (OperationCanceledException)
        {
            // quiet stop on cancellation
        }
    }

    /// <summary>
    /// Continuously reads stderr in chunks until the stream ends or cancellation is requested.
    /// Invokes <see cref="OnStderr"/> for each chunk.
    /// </summary>
    private async Task ReadStderrLoop(CancellationToken cancellationToken = default)
    {
        var stream = _process.StandardError.BaseStream;
        var buffer = new byte[BufferSize];

        try
        {
            int read;
            while ((read = await stream.ReadAsync(buffer, cancellationToken)) > 0)
            {
                OnStderr?.Invoke(buffer.AsSpan(0, read).ToArray());
            }
        }
        catch (OperationCanceledException)
        {
            // quiet stop on cancellation
        }
    }

    /// <summary>
    /// Attempts to kill the process and its entire process tree.
    /// Exceptions are swallowed because the process may already be terminating.
    /// </summary>
    public void Kill()
    {
        try
        {
            _process.Kill(entireProcessTree: true);
            ExitTime = _process.ExitTime;
        }
        catch
        {
        }
    }

    /// <summary>
    /// Asynchronously waits for the process to exit.
    /// </summary>
    /// <param name="cancellationToken">
    /// Cancels the wait operation.  
    /// Does not kill the process; use <see cref="Kill"/> to terminate it.
    /// </param>
    /// <returns>The process exit code.</returns>
    public async Task<int> WaitForExitAsync(CancellationToken cancellationToken = default)
    {
        await _process.WaitForExitAsync(cancellationToken);
        ExitTime = _process.ExitTime;
        return _process.ExitCode;
    }

    /// <summary>
    /// Disposes the underlying process object and resets the PID.
    /// </summary>
    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        _killRegistration?.Dispose();
        Kill();
        try
        {
            if (_outputTask is not null)
                await _outputTask;
        }
        catch
        { }
        _process.Dispose();
        Pid = -1;
    }
}
