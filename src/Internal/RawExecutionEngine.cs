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

    private long _totalReadBytes;
    private int _readCount;
    private string _logPrefix => $"[{_runner.Pid}][{CommandPath}]";

    public string CommandPath { get; }
    public string[] Arguments { get; }
    public Encoding Encoding { get; }
    public bool AsString { get; }
    public OutputType OutputType { get; }

    public DateTime StartTime => _runner.StartTime;
    public DateTime ExitTime => _runner.ExitTime;

    public BlockingCollection<RawOutputItem> Output { get; } = new(1024);

    public RawExecutionEngine(InvokeRawCommandCommand cmdlet, string commandPath) : base(cmdlet)
    {
        CommandPath = commandPath;
        Arguments = cmdlet.Arguments;
        Encoding = EncodingCompleter.GetEncoding(cmdlet.Encoding);
        AsString = cmdlet.AsString.ToBool();
        OutputType = cmdlet.Output;
        _runner = new RawProcessRunner(CommandPath, Arguments);
    }

    public override void BeginProcessing()
    {
        StartAsync(PipelineStopToken);
        WriteVerboseRaw($"{_logPrefix} Started process with arguments: [{string.Join(", ", Arguments)}] ({StartTime.ToLocalTime():HH:mm:ss.fff})");
    }

    public override void ProcessRecord(byte[] inputBytes)
    {
        WriteInputAsync(inputBytes, PipelineStopToken).Wait();
    }

    public override void StopProcessing()
    {
        WriteVerboseRaw($"{_logPrefix} Stopping process");
        Kill();

        PrintDebugMessages();
    }

    public override void EndProcessing() => WaitForExecute();

    private void StartAsync(CancellationToken cancellationToken)
    {
        if (AsString)
        {
            if (OutputType.HasFlag(OutputType.Stdout))
            {
                _runner.OnStdout += OnOutputChunkAsString;
                _stdoutDecoder ??= new(Encoding, Output, OutputType.Stdout);
            }
            if (OutputType.HasFlag(OutputType.Stderr))
            {
                _runner.OnStderr += OnOutputChunkAsString;
                _stdoutDecoder ??= new(Encoding, Output, OutputType.Stdout);
            }
            else
            {
                _runner.OnStderr += OnErrorChunkAsString;
                _stderrDecoder ??= new(Encoding, Output, OutputType.Stderr);
            }
        }
        else
        {
            if (OutputType.HasFlag(OutputType.Stdout))
            {
                _runner.OnStdout += OnOutputChunk;
            }
            if (OutputType.HasFlag(OutputType.Stderr))
            {
                _runner.OnStderr += OnOutputChunk;
            }
            else
            {
                _runner.OnStderr += OnErrorChunkAsString;
                _stderrDecoder ??= new(Encoding, Output, OutputType.Stderr);
            }
        }
        _runner.Start(cancellationToken);
    }

    private void OnOutputChunk(byte[] chunk)
    {
        Output.Add(new ChunkOutput(chunk, OutputType.Stdout));
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
        Output.Add(new ChunkOutput(chunk, OutputType.Stderr));
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

    private void Kill()
    {
        _runner.Kill();
        _stdoutDecoder?.DisposeAsync().AsTask().Wait();
        _stderrDecoder?.DisposeAsync().AsTask().Wait();
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

    private void WaitForExecute()
    {
        var exitTask = WaitForExitAsync(PipelineStopToken);
        long totalWriteBytes = 0;
        int writeCount = 0;
        int lineCount = 0;
        foreach (var output in Output.GetConsumingEnumerable(PipelineStopToken))
        {
            switch (output.To)
            {
                case OutputType.Stdout:
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
                    break;
                case OutputType.Stderr:
                    InformationRecord record;
                    switch (output)
                    {
                        case StringOutput line:
                            PrintDebug($"[{Cmdlet.MyCommandName}] Error line: {line.Value}");
                            record = new InformationRecord($"{PSStyle.Instance.Formatting.Error}{line.Value}{PSStyle.Instance.Reset}",
                                                           $"{_runner.Name} (PID: {_runner.Pid})");
                            record.Tags.AddRange("PSHOST", "stderr");
                            WriteInformation(record);
                            break;
                        case ChunkOutput chunk:
                            PrintDebug($"[{Cmdlet.MyCommandName}] Error chunk: {chunk.Value.Length} bytes");
                            record = new InformationRecord($"{PSStyle.Instance.Formatting.Error}{string.Join(':', chunk.Value.Select(b => b.ToString("X2")))}{PSStyle.Instance.Reset}",
                                                           $"{_runner.Name} (PID: {_runner.Pid})");
                            record.Tags.AddRange("PSHOST", "stderr");
                            WriteInformation(record);
                            break;
                    }
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
