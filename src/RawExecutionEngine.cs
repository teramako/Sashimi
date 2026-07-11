using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO.Pipes;
using System.Management.Automation;
using System.Runtime.CompilerServices;
using System.Text;

namespace Sashimi;

internal sealed class RawExecutionEngine : ExecutionEngine
{
    private readonly RawProcessRunner _runner;
    private AnonymousPipeServerStream? _stringServer;
    private AnonymousPipeClientStream? _stringClient;
    private Task? _stringReaderTask;
    private Decoder _stderrDecoder;
    private long _totalReadBytes;
    private int _readCount;
    private string _logPrefix => $"[{_runner.Pid}][{CommandPath}]";

    public string CommandPath { get; }
    public string[] Arguments { get; }
    public Encoding Encoding { get; }
    [MemberNotNullWhen(true, nameof(_stringServer), nameof(_stringClient), nameof(_stringReaderTask))]
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
        _stderrDecoder = Encoding.GetDecoder();
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
            _stringServer = new(PipeDirection.Out, HandleInheritability.None);
            _stringClient = new(PipeDirection.In, _stringServer.ClientSafePipeHandle);
            if (OutputType.HasFlag(OutputType.Stdout))
            {
                _runner.OnStdout += OnOutputChunkAsString;
            }
            if (OutputType.HasFlag(OutputType.Stderr))
            {
                _runner.OnStderr += OnOutputChunkAsString;
            }
            else
            {
                _runner.OnStderr += OnErrorChunk;
            }
            _stringReaderTask = AsyncDecode(_stringClient, Encoding);
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
                _runner.OnStderr += OnErrorChunk;
            }
        }
        _runner.Start(cancellationToken);
    }

    private void OnOutputChunk(byte[] chunk)
    {
        Output.Add(new ChunkOutput(chunk));
    }

    private void OnOutputChunkAsString(byte[] chunk)
    {
        if (chunk.Length > 0 && _stringServer is not null)
        {
            _stringServer.Write(chunk, 0, chunk.Length);
            PrintDebug($"Write {chunk.Length} bytes to stringServer");
            _stringServer.Flush();
        }
    }

    private void OnErrorChunk(byte[] chunk)
    {
        if (_stderrDecoder is null)
            return;

        PrintDebug($"Write StdErr: {chunk.Length} bytes");
        var charCount = _stderrDecoder.GetCharCount(chunk, false);
        Span<char> text = stackalloc char[charCount];
        _stderrDecoder.Convert(chunk, text, false, out _, out var charsUsed, out _);
        var record = new InformationRecord($"{PSStyle.Instance.Formatting.Error}{text[..charsUsed].TrimEnd("\r\n")}{PSStyle.Instance.Reset}",
                                           $"{_runner.Name} (PID: {_runner.Pid})");
        record.Tags.AddRange("PSHOST", "stderr");
        Output.Add(new InformationOutput(record));

        // NOTE:
        // We intentionally do NOT flush the stderr decoder at stream end.
        // Any remaining bytes in the decoder represent incomplete or undecodable sequences.
        // Flushing would emit fallback characters, which is undesirable:
        // stderr should show only successfully decoded text, and undecodable bytes are ignored rather than replaced.
    }

    private async Task AsyncDecode(Stream stream, Encoding encoding)
    {
        using var sr = new StreamReader(stream, encoding);
        while (true)
        {
            string? line = await sr.ReadLineAsync();
            if (line is null)
                break;

            PrintDebug("Set string line");
            Output.Add(new StringOutput(line));
        }

        var rest = await sr.ReadToEndAsync();
        if (!string.IsNullOrEmpty(rest))
        {
            Output.Add(new StringOutput(rest));
        }
        PrintDebug("Complete stringReader");
        Output.CompleteAdding();
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
        _stringServer?.Dispose();
        _stringClient?.Dispose();
    }

    private async Task<int> WaitForExitAsync(CancellationToken cancellationToken)
    {
        _runner.CloseStdin();

        int exitCode;
        PrintDebug($"Wait process runner's output to finish");
        exitCode = await _runner.WaitForCompleteAsync(cancellationToken);

        if (AsString)
        {
            _stringServer.Close();
            await _stringReaderTask;
        }
        else
        {
            PrintDebug("Complete queueInput");
            Output.CompleteAdding();
        }
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
                case InformationOutput info:
                    PrintDebug($"[{Cmdlet.MyCommandName}] Output Information: {info.Value}");
                    WriteInformation(info.Value);
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
