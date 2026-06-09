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

    public async Task StartAsync()
    {
        _process.Start();
        _ = Task.Run(ReadStdoutLoop);
        _ = Task.Run(ReadStderrLoop);
        Pid = _process.Id;
    }
    
    public Task WriteStdinAsync(byte[] buffer)
    {
        return _process.StandardInput.BaseStream.WriteAsync(buffer, 0, buffer.Length);
    }
    
    public void CloseStdin()
    {
        _process.StandardInput.Close();
    }
    
    private async Task ReadStdoutLoop()
    {
        var stream = _process.StandardOutput.BaseStream;
        var buffer = new byte[BufferSize];

        int read;
        while ((read = await stream.ReadAsync(buffer)) > 0)
        {
            OnStdout?.Invoke(buffer.AsSpan(0, read).ToArray());
        }
    }
    
    private async Task ReadStderrLoop()
    {
        var stream = _process.StandardError.BaseStream;
        var buffer = new byte[BufferSize];

        int read;
        while ((read = await stream.ReadAsync(buffer)) > 0)
        {
            OnStderr?.Invoke(buffer.AsSpan(0, read).ToArray());
        }
    }
    
    public async Task<int> WaitForExitAsync()
    {
        await _process.WaitForExitAsync();
        return _process.ExitCode;
    }

    public ValueTask DisposeAsync()
    {
        _process.Dispose();
        Pid = -1;
        return ValueTask.CompletedTask;
    }
}
