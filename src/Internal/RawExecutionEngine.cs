using System.Collections.Concurrent;
using System.Management.Automation;
using System.Text;

namespace Sashimi.Internal;

internal class RawExecutionEngine(RawCommandBase cmdlet,
                                  string commandPath,
                                  string[] arguments,
                                  Redirection redirection,
                                  Encoding encoding,
                                  bool asString = false,
                                  bool throwOnNonZeroExitCode = false)
    : ExecutionEngine(cmdlet)
{
    protected RawProcessRunner Runner { get; } = new(commandPath,
                                                     arguments,
                                                     cmdlet.SessionState.Path.CurrentFileSystemLocation.Path,
                                                     cmdlet.MyInvocation.HistoryId);
    protected string CommandPath { get; } = commandPath;
    protected string[] Arguments { get; } = arguments;
    protected Encoding Encoding { get; } = encoding;
    protected bool AsString { get; } = asString;
    protected bool ThrowOnNonZeroExitCode { get; } = throwOnNonZeroExitCode;

    private ChunkStringDecoder? _stdoutDecoder;
    private ChunkStringDecoder? _stderrDecoder;
    private Redirection _redirection = redirection;

    private long _totalReadBytes;
    private int _readCount;
    private string _logPrefix => $"[{Runner.Pid}][{CommandPath}]";

    public DateTime StartTime => Runner.StartTime;
    public DateTime ExitTime => Runner.ExitTime;

    public BlockingCollection<RawOutputRecord> Output { get; } = new(1024);

    public override void BeginProcessing()
    {
        try
        {
            StartAsync(PipelineStopToken);
            WriteVerboseRaw($"{_logPrefix} Started process with arguments: [{string.Join(", ", Arguments)}] ({StartTime.ToLocalTime():HH:mm:ss.fff})");
            WriteVerboseRaw($"Stdout -> {_redirection.StdoutTo}, Stderr -> {_redirection.StderrTo}");
        }
        catch (Exception ex)
        {
            Runner.DebugLog(ex, "exception");
            throw;
        }
        finally
        {
            Cmdlet.FlushDebugMessages();
        }
    }

    public override void ProcessRecord(byte[] inputBytes)
    {
        WriteInputAsync(inputBytes, PipelineStopToken).Wait();
    }

    public override void ProcessRecord(string inputString)
    {
        ProcessRecord(Encoding.GetBytes(inputString));
    }

    public override void StopProcessing()
    {
        WriteVerboseRaw($"{_logPrefix} Stopping process");
        KillAsync().GetAwaiter().GetResult();

        Cmdlet.FlushDebugMessages();
    }

    public override void EndProcessing()
    {
        var exitTask = WaitForExitAsync(PipelineStopToken);

        OutputRecords();

        try
        {
            var exitCode = exitTask.GetAwaiter().GetResult();
            WriteVerboseRaw($"{_logPrefix} End [ExitCode = {exitCode}]"
                            + $" ({ExitTime.ToLocalTime():HH:mm:ss.fff},"
                            + $" Duration={ExitTime - StartTime}))");
            Cmdlet.SetLastExitCode(exitCode);

            if (exitCode != 0 && ThrowOnNonZeroExitCode)
            {
                throw new ExternalCommandNonZeroExitException($"'{CommandPath}' exited with {exitCode}", exitCode);
            }
        }
        catch (Exception ex)
        {
            Runner.DebugLog(ex, "exception");
            throw;
        }
        finally
        {
            Cmdlet.FlushDebugMessages();
        }
    }

    private void StartAsync(CancellationToken cancellationToken)
    {
        if (AsString)
        {
            if (_redirection.StdoutTo is not RedirectTo.Null)
            {
                Runner.OnStdout += OnOutputChunkAsString;
                _stdoutDecoder = new(Cmdlet, Encoding, line => Output.Add(new StringOutput(line.ToString(), _redirection.StdoutTo, OutputFrom.Stdout)));
            }

            if (_redirection.StderrTo is not RedirectTo.Null)
            {
                Runner.OnStderr += OnErrorChunkAsString;
                _stderrDecoder = new(Cmdlet, Encoding, line => Output.Add(new StringOutput(line.ToString(), _redirection.StderrTo, OutputFrom.Stderr)));
            }
        }
        else
        {
            if (_redirection.StdoutTo is not RedirectTo.Null)
            {
                Runner.OnStdout += OnOutputChunk;
            }

            if (_redirection.StderrTo is not RedirectTo.Null)
            {
                if (_redirection.StderrTo is RedirectTo.Output)
                {
                    Runner.OnStderr += OnErrorChunk;
                }
                else
                {
                    Runner.OnStderr += OnErrorChunkAsString;
                    _stderrDecoder = new(Cmdlet, Encoding, line => Output.Add(new StringOutput(line.ToString(), _redirection.StderrTo, OutputFrom.Stderr)));
                }
            }
        }
        Runner.Start(cancellationToken);
    }

    private void OnOutputChunk(byte[] chunk)
    {
        Output.Add(new ChunkOutput(chunk, _redirection.StdoutTo, OutputFrom.Stdout));
    }

    private void OnOutputChunkAsString(byte[] chunk)
    {
        if (chunk.Length > 0 && _stdoutDecoder is not null)
        {
            _stdoutDecoder.Decode(chunk);
            Cmdlet.DebugLog($"Write {chunk.Length} bytes to StdOut");
        }
    }

    private void OnErrorChunk(byte[] chunk)
    {
        Output.Add(new ChunkOutput(chunk, _redirection.StderrTo, OutputFrom.Stderr));
    }

    private void OnErrorChunkAsString(byte[] chunk)
    {
        if (chunk.Length > 0 && _stderrDecoder is not null)
        {
            _stderrDecoder.Decode(chunk);
            Cmdlet.DebugLog($"Write {chunk.Length} bytes to StdErr");
        }
    }

    private async Task WriteInputAsync(byte[] inputBytes, CancellationToken cancellationToken)
    {
        _totalReadBytes += inputBytes.Length;
        _readCount++;
        Cmdlet.DebugLog($"Read {inputBytes.Length} bytes from pipeline");
        await Runner.WriteStdinAsync(inputBytes, cancellationToken);
    }

    private async Task KillAsync()
    {
        Runner.Kill();
    }

    protected async Task<int> WaitForExitAsync(CancellationToken cancellationToken)
    {
        Runner.CloseStdin();

        int exitCode;
        Cmdlet.DebugLog($"Wait process runner's output to finish");
        exitCode = await Runner.WaitForCompleteAsync(cancellationToken);

        _stdoutDecoder?.EmitRemaining();
        _stderrDecoder?.EmitRemaining();

        Cmdlet.DebugLog("Complete queueInput");
        Output.CompleteAdding();

        return exitCode;
    }

    protected virtual void OutputRecords()
    {
        long totalWriteBytes = 0;
        int writeCount = 0;
        int lineCount = 0;
        foreach (var output in Output.GetConsumingEnumerable(PipelineStopToken))
        {
            switch (output.To)
            {
                case RedirectTo.Output:
                    WriteOut(output);
                    break;
                case RedirectTo.Error:
                    WriteError(output);
                    break;
                case RedirectTo.Warning: // `n>&3`:
                    // Since PowerShell core does not support, this branch will likely never be entered.
                    Cmdlet.WriteWarning(output.ToString());
                    break;
                case RedirectTo.Verbose: // `n>&4`:
                    // Since PowerShell core does not support, this branch will likely never be entered.
                    Cmdlet.WriteVerbose(output.ToString());
                    break;
                case RedirectTo.Debug: // `n>&5`:
                    // Since PowerShell core does not support, this branch will likely never be entered.
                    Cmdlet.WriteDebug(output.ToString());
                    break;
                case RedirectTo.Information: // `n>&6`:
                    // Since PowerShell core does not support, this branch will likely never be entered.
                    InformationRecord record = new(output, $"{Runner.Name} (PID: {Runner.Pid})");
                    record.Tags.AddRange("PSHOST", "redirect");
                    Cmdlet.WriteInformation(record);
                    break;
            }
        }
        if (lineCount > 0)
        {
            WriteVerboseRaw($"{_logPrefix} Output total line: {lineCount}");
        }
        if (totalWriteBytes > 0)
        {
            WriteVerboseRaw($"{_logPrefix} Output total: {totalWriteBytes}, count: {writeCount}");
        }

        void WriteOut(RawOutputRecord output)
        {
            switch (output)
            {
                case StringOutput line:
                    lineCount++;
                    Cmdlet.DebugLog($"[{Cmdlet.MyCommandName}] Output line: [{lineCount}] {line.Value}");
                    WriteObject(line.Value);
                    break;
                case ChunkOutput chunk:
                    totalWriteBytes += chunk.Value.Length;
                    writeCount++;
                    Cmdlet.DebugLog($"[{Cmdlet.MyCommandName}] Output chunk: {chunk.Value.Length} bytes");
                    WriteObject(chunk.Value, false);
                    break;
            }
        }

        void WriteError(RawOutputRecord output)
        {
#if DEBUG
            switch (output)
            {
                case StringOutput line:
                    Cmdlet.DebugLog($"[{Cmdlet.MyCommandName}] Error line: {line.Value}");
                    break;
                case ChunkOutput chunk:
                    Cmdlet.DebugLog($"[{Cmdlet.MyCommandName}] Error chunk: {chunk.Value.Length} bytes");
                    break;
            }
#endif
            ErrorRecord error = new(new RemoteException(output.ToString()), "ExternalCommandError", ErrorCategory.FromStdErr, output);
            Cmdlet.WriteError(error);
        }
    }
}
