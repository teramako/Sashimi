using System.Collections.ObjectModel;
using System.Diagnostics;

namespace Sashimi;

public sealed class RawProcessRunner : IAsyncDisposable
{
    public static ProcessStartInfo CreateProcessStartInfo(string fileName, IEnumerable<string> arguments)
        => new(fileName, arguments)
        {
            UseShellExecute = false,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };

    public RawProcessRunner(string fileName, IEnumerable<string> arguments)
    {
        var psi = CreateProcessStartInfo(fileName, arguments);
        _process = new() { StartInfo = psi };
        Arguments = psi.ArgumentList.AsReadOnly();
    }

    private readonly Process _process;
    private const int BufferSize = 4096;
    private Task _outputTask = null!;

    public string Name => _process.StartInfo.FileName;
    public int Pid
    {
        get => field == -1
               ? throw new InvalidOperationException("Process has not been started yet.")
               : field;
        private set;
    } = -1;
    public ReadOnlyCollection<string> Arguments { get; }

    public event Action<byte[]>? OnStdout;
    public event Action<byte[]>? OnStderr;

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        _process.Start();
        _outputTask = Task.WhenAll(ReadStdoutLoop(cancellationToken),
                                   ReadStderrLoop(cancellationToken));
        Pid = _process.Id;
    }
    
    public Task WriteStdinAsync(byte[] buffer)
    {
        return _process.StandardInput.BaseStream.WriteAsync(buffer, 0, buffer.Length);
    }

    public void WaitOutput(CancellationToken cancellationToken = default) => _outputTask.Wait(cancellationToken);
    
    public void CloseStdin()
    {
        _process.StandardInput.Close();
    }
    
    private async Task ReadStdoutLoop(CancellationToken cancellationToken = default)
    {
        var stream = _process.StandardOutput.BaseStream;
        var buffer = new byte[BufferSize];

        int read;
        while ((read = await stream.ReadAsync(buffer, cancellationToken)) > 0)
        {
            OnStdout?.Invoke(buffer.AsSpan(0, read).ToArray());
        }
    }
    
    private async Task ReadStderrLoop(CancellationToken cancellationToken = default)
    {
        var stream = _process.StandardError.BaseStream;
        var buffer = new byte[BufferSize];

        int read;
        while ((read = await stream.ReadAsync(buffer, cancellationToken)) > 0)
        {
            OnStderr?.Invoke(buffer.AsSpan(0, read).ToArray());
        }
    }

    public void Kill()
    {
        try
        {
            _process.Kill(entireProcessTree: true);
        }
        catch
        {
        }
    }
    
    public async Task<int> WaitForExitAsync(CancellationToken cancellationToken = default)
    {
        await _process.WaitForExitAsync(cancellationToken);
        return _process.ExitCode;
    }

    public ValueTask DisposeAsync()
    {
        _process.Dispose();
        Pid = -1;
        return ValueTask.CompletedTask;
    }
}
