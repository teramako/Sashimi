using System.Collections.Concurrent;
using System.Diagnostics;
using System.Management.Automation;
using System.Runtime.CompilerServices;
using System.Text;

namespace Sashimi.Internal;

internal sealed class RawExecutionEngine : ExecutionEngine
{
    private readonly RawProcessRunner _runner;
    private PipeStringDecoder? _stdoutDecoder;
    private PipeStringDecoder? _stderrDecoder;
    private Redirection _redirection;

    private long _totalReadBytes;
    private int _readCount;
    private string _logPrefix => $"[{_runner.Pid}][{CommandPath}]";

    public string CommandPath { get; }
    public string[] Arguments { get; }
    public Encoding Encoding { get; }
    public bool AsString { get; }

    public DateTime StartTime => _runner.StartTime;
    public DateTime ExitTime => _runner.ExitTime;

    public BlockingCollection<RawOutputRecord> Output { get; } = new(1024);

    public RawExecutionEngine(InvokeRawCommandCommand cmdlet, string commandPath) : base(cmdlet)
    {
        CommandPath = commandPath;
        Arguments = cmdlet.Arguments;
        Encoding = EncodingCompleter.GetEncoding(cmdlet.Encoding);
        AsString = cmdlet.AsString.ToBool();
        _redirection = Redirection.GetRedirectionFromStatement(cmdlet.MyInvocation.Statement, cmdlet.Output);
        _runner = new RawProcessRunner(CommandPath, Arguments);
    }

    public override void BeginProcessing()
    {
        StartAsync(PipelineStopToken);
        WriteVerboseRaw($"{_logPrefix} Started process with arguments: [{string.Join(", ", Arguments)}] ({StartTime.ToLocalTime():HH:mm:ss.fff})");
        WriteVerboseRaw($"Stdout -> {_redirection.StdoutTo}, Stderr -> {_redirection.StderrTo}");
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

        PrintDebugMessages();
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
            Cmdlet.SessionState.PSVariable.Set("LASTEXITCODE", exitCode);

        }
        catch
        {
            Cmdlet.SessionState.PSVariable.Set("LASTEXITCODE", 1);
            throw;
        }
        finally
        {
            PrintDebugMessages();
        }
    }

    private void StartAsync(CancellationToken cancellationToken)
    {
        if (AsString)
        {
            if (_redirection.StdoutTo is not RedirectTo.Null)
            {
                _runner.OnStdout += OnOutputChunkAsString;
                _stdoutDecoder ??= new(Encoding, Output, _redirection.StdoutTo);
            }

            if (_redirection.StderrTo is not RedirectTo.Null)
            {
                _runner.OnStderr += OnErrorChunkAsString;
                _stderrDecoder ??= new(Encoding, Output, _redirection.StderrTo);
            }
        }
        else
        {
            if (_redirection.StdoutTo is not RedirectTo.Null)
            {
                _runner.OnStdout += OnOutputChunk;
            }

            if (_redirection.StderrTo is not RedirectTo.Null)
            {
                if (_redirection.StderrTo is RedirectTo.Output)
                {
                    _runner.OnStderr += OnErrorChunk;
                }
                else
                {
                    _runner.OnStderr += OnErrorChunkAsString;
                    _stderrDecoder ??= new(Encoding, Output, _redirection.StderrTo);
                }
            }
        }
        _runner.Start(cancellationToken);
    }

    private void OnOutputChunk(byte[] chunk)
    {
        Output.Add(new ChunkOutput(chunk, _redirection.StdoutTo));
    }

    private void OnOutputChunkAsString(byte[] chunk)
    {
        if (chunk.Length > 0 && _stdoutDecoder is not null)
        {
            _stdoutDecoder.WriteBytes(chunk);
            PrintDebug($"Write {chunk.Length} bytes to StdOut pipe");
        }
    }

    private void OnErrorChunk(byte[] chunk)
    {
        Output.Add(new ChunkOutput(chunk, _redirection.StderrTo));
    }

    private void OnErrorChunkAsString(byte[] chunk)
    {
        if (chunk.Length > 0 && _stderrDecoder is not null)
        {
            _stderrDecoder.WriteBytes(chunk);
            PrintDebug($"Write {chunk.Length} bytes to StdErr pipe");
        }
    }

    private async Task WriteInputAsync(byte[] inputBytes, CancellationToken cancellationToken)
    {
        _totalReadBytes += inputBytes.Length;
        _readCount++;
        PrintDebug($"Read {inputBytes.Length} bytes from pipeline");
        await _runner.WriteStdinAsync(inputBytes, cancellationToken);
    }

    private async Task KillAsync()
    {
        _runner.Kill();
        try
        {
            await (_stdoutDecoder?.DisposeAsync() ?? ValueTask.CompletedTask);
        }
        catch
        { }

        try
        {
            await (_stderrDecoder?.DisposeAsync() ?? ValueTask.CompletedTask);
        }
        catch
        { }
    }

    private async Task<int> WaitForExitAsync(CancellationToken cancellationToken)
    {
        _runner.CloseStdin();

        int exitCode;
        PrintDebug($"Wait process runner's output to finish");
        exitCode = await _runner.WaitForCompleteAsync(cancellationToken);

        await (_stdoutDecoder?.DisposeAsync() ?? ValueTask.CompletedTask);
        await (_stderrDecoder?.DisposeAsync() ?? ValueTask.CompletedTask);

        PrintDebug("Complete queueInput");
        Output.CompleteAdding();

        return exitCode;
    }

    private void OutputRecords()
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
                    InformationRecord record = new(output, $"{_runner.Name} (PID: {_runner.Pid})");
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
                    PrintDebug($"[{Cmdlet.MyCommandName}] Output line: [{lineCount}] {line.Value}");
                    WriteObject(line.Value);
                    break;
                case ChunkOutput chunk:
                    totalWriteBytes += chunk.Value.Length;
                    writeCount++;
                    PrintDebug($"[{Cmdlet.MyCommandName}] Output chunk: {chunk.Value.Length} bytes");
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
                    PrintDebug($"[{Cmdlet.MyCommandName}] Error line: {line.Value}");
                    break;
                case ChunkOutput chunk:
                    PrintDebug($"[{Cmdlet.MyCommandName}] Error chunk: {chunk.Value.Length} bytes");
                    break;
            }
#endif
            ErrorRecord error = new(new RemoteException(output.ToString()), "NativeCommandError", ErrorCategory.FromStdErr, output);
            Cmdlet.WriteError(error);
        }
    }

    [Conditional("DEBUG")]
    public void PrintDebug(string msg,
                           [CallerMemberName] string callerMethodName = "",
                           [CallerLineNumber] int callerLineNumber = 0)
    {
        _runner.Log($"{msg}", "cmdlet", callerMethodName, callerLineNumber);
    }

    [Conditional("DEBUG")]
    public void PrintDebugMessages()
    {
#if DEBUG
        foreach (var msg in _runner.DebugMsgs)
        {
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.Error.WriteLine(msg);
        }
        Console.ResetColor();
#endif
    }
}
