using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace Sashimi;

/// <summary>
/// Runs an external process and exposes its stdin/stdout/stderr as asynchronous streams.
/// Designed for PowerShell cmdlets that require non-blocking, cancellable process I/O.
/// </summary>
public sealed class RawProcessRunner : IAsyncDisposable
{
#if DEBUG
    public record DebugMsg(TimeSpan TimeSpan, string Category, string Source, object Message);
    private readonly Stopwatch _sw = new();
    private readonly ConcurrentQueue<DebugMsg> _messages = new();
    public DebugMsg[] DebugMsgs => _messages.ToArray();
#endif

    [Conditional("DEBUG")]
    public void Log(object msg, string category, [CallerMemberName] string callerMethodName = "", [CallerLineNumber] int callerLineNumber = 0)
    {
        _messages.Enqueue(new(_sw.Elapsed, category, $"{callerMethodName}:{callerLineNumber}", msg));
    }

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
#if DEBUG
        _sw.Start();
#endif
        Log("Started", "process");
        try
        {
            StartTime = _process.StartTime.ToUniversalTime();
        }
        catch
        {
            // Fallback — process info not yet available (race on /proc). Use UtcNow.
            StartTime = DateTime.UtcNow;
        }

        // Ensure the process terminates properly upon cancellation
        _killRegistration = cancellationToken.Register(() =>
        {
            Log("Killing on cancellationToken", "lifecycle");
            Kill();
        });

        _outputTask = Task.Run(async () =>
            await Task.WhenAll(ReadStdoutLoop(cancellationToken),
                               ReadStderrLoop(cancellationToken)));

        Pid = _process.Id;
    }

    /// <summary>
    /// Writes raw bytes to the process's standard input stream.
    /// </summary>
    public Task WriteStdinAsync(byte[] buffer, CancellationToken cancellationToken = default)
    {
        Log($"Read StdIn: {buffer.Length} bytes", "stdin");
        return _process.StandardInput.BaseStream.WriteAsync(buffer, 0, buffer.Length, cancellationToken);
    }

    /// <summary>
    /// Closes the process's standard input stream, signaling end-of-input.
    /// </summary>
    public void CloseStdin()
    {
        Log("Close StdIn", "lifecycle");
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
                Log($"OnStdout: {read} bytes", "stdout");
                OnStdout?.Invoke(buffer.AsSpan(0, read).ToArray());
            }
            Log($"End OnStdout", "stderr");
        }
        catch (OperationCanceledException ex)
        {
            Log(ex, "exception");
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
                Log($"OnStderr: {read} bytes", "stderr");
                OnStderr?.Invoke(buffer.AsSpan(0, read).ToArray());
            }
            Log($"End OnStderr", "stderr");
        }
        catch (OperationCanceledException ex)
        {
            Log(ex, "exception");
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
            ExitTime = _process.ExitTime.ToUniversalTime();
            Log("Killed", "process");
        }
        catch(Exception ex)
        {
            Log(ex, "exception");
        }
    }

    /// <summary>
    /// Blocks until both stdout and stderr reading tasks complete.
    /// <para>
    /// This ensures that all output has been fully read before closing the underlying streams.
    /// Closing the streams only after the read loops finish prevents race conditions and
    /// ObjectDisposedException that can occur if the streams are closed prematurely.
    /// </para>
    /// </summary>
    /// <param name="cancellationToken">
    /// Cancels the wait operation.  
    /// Does not kill the process; use <see cref="Kill"/> for that.
    /// </param>
    public async Task WaitOutputAsync(CancellationToken cancellationToken = default)
    {
        Log("Waiting end of output ...", "lifecycle");
        if (_outputTask is not null)
        {
            try
            {
                await _outputTask.WaitAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                Log(ex, "exception");
                throw;
            }
        }

        try
        {
            _process.StandardOutput.BaseStream.Close();
            Log("Closed stdout", "lifecycle");
        }
        catch (Exception ex)
        {
            Log(ex, "stdout");
        }

        try
        {
            _process.StandardError.BaseStream.Close();
            Log("Closed stderr", "lifecycle");
        }
        catch (Exception ex)
        {
            Log(ex, "stderr");
        }
    }

    /// <summary>
    /// Asynchronously waits for the process to exit.
    /// <para>
    /// This does not wait for stdout/stderr to finish reading; use WaitOutputAsync or
    /// WaitForCompleteAsync to ensure that all output has been fully consumed.
    /// </para>
    /// </summary>
    /// <param name="cancellationToken">
    /// Cancels the wait operation.  
    /// Does not kill the process; use <see cref="Kill"/> to terminate it.
    /// </param>
    /// <returns>The process exit code.</returns>
    public async Task<int> WaitForExitAsync(CancellationToken cancellationToken = default)
    {
        Log("Waiting for exit...", "lifecycle");
        await _process.WaitForExitAsync(cancellationToken);
        ExitTime = _process.ExitTime.ToUniversalTime();
        Log($"Exit [{_process.ExitCode}]", "process");
        return _process.ExitCode;
    }

    /// <summary>
    /// Waits for both the process to exit and all stdout/stderr output to be fully consumed.
    /// <para>
    /// This method guarantees the correct completion order:
    /// <list type="number">
    ///     <item>Process exit</item>
    ///     <item>stdout/stderr read loops finish (EOF)</item>
    ///     <item>Streams are safely closed</item>
    /// </list>
    /// When using PipeStream, this method provides the only safe way to ensure that
    /// stringReaderTask can observe EOF and terminate without deadlocks or race conditions.
    /// </para>
    /// </summary>
    /// <param name="cancellationToken">
    /// Cancels the wait operation.  
    /// Does not kill the process; use <see cref="Kill"/> to terminate it.
    /// </param>
    /// <returns>The process exit code.</returns>
    public async Task<int> WaitForCompleteAsync(CancellationToken cancellationToken = default)
    {
        var exitCode = await WaitForExitAsync(cancellationToken);
        await WaitOutputAsync(cancellationToken);
        return exitCode;
    }

    /// <summary>
    /// Disposes the underlying process object and resets the PID.
    /// </summary>
    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        Log("Killing on disposint", "lifecycle");
        _killRegistration?.Dispose();
        Kill();
        try
        {
            if (_outputTask is not null)
                await _outputTask;
        }
        catch(Exception ex)
        {
            Log(ex, "exception");
        }
        _process.Dispose();
        Pid = -1;
    }
}
