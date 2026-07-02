using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO.Pipes;
using System.Management.Automation;
using System.Management.Automation.Language;
using System.Runtime.CompilerServices;
using System.Text;

namespace Sashimi;

[Flags]
public enum OutputType
{
    Stdout = 1,
    Stderr = 2,
    Both = Stdout | Stderr
}

internal abstract record RawOutputItem;
internal record ChunkOutput(byte[] Value) : RawOutputItem;
internal record StringOutput(string Value) : RawOutputItem;
internal record InformationOutput(InformationRecord Value) : RawOutputItem;

[Cmdlet(VerbsLifecycle.Invoke, "RawCommand", DefaultParameterSetName = NormalParameterSet)]
[Alias("raw")]
[OutputType(typeof(byte[]))]
[OutputType(typeof(string))]
public class InvokeRawCommandCommand : RawCommandBase
{
    private const string NormalParameterSet = "Normal";
    private const string ScriptBlockParameterSet = "ScriptBlock";

    [Parameter(ParameterSetName = NormalParameterSet, Mandatory = true, Position = 0)]
    public string? Command { get; set; }

    [Parameter(ParameterSetName = NormalParameterSet, ValueFromRemainingArguments = true, Position = 1)]
    public string[] Arguments { get; set; } = [];

    [Parameter(ParameterSetName = ScriptBlockParameterSet, Mandatory = true, Position = 0)]
    public ScriptBlock? Script { get; set; }

    [Parameter(ValueFromPipeline = true)]
    public byte[]? InputBytes { get; set; }

    [Parameter()]
    [Alias("o")]
    public OutputType Output { get; set; } = OutputType.Stdout;

    [Parameter()]
    [Alias("s")]
    public SwitchParameter AsString { get; set; }

    [Parameter()]
    [Alias("e")]
    public string Encoding { get; set; } = "UTF-8";

    private Encoding _encoding = System.Text.Encoding.UTF8;
    private Decoder? _stderrDecoder;

    private RawProcessRunner _processRunner = null!;

    private readonly BlockingCollection<RawOutputItem> _output = new(1024);

    private long _totalReadBytes;
    private int _readCount;

    private AnonymousPipeServerStream? _stringServer;
    private AnonymousPipeClientStream? _stringClient;
    private Task? _stringReaderTask;

    private void OnOutputChunk(byte[] chunk)
    {
        _output.Add(new ChunkOutput(chunk));
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
                                           $"{_processRunner.Name} (PID: {_processRunner.Pid})");
        record.Tags.AddRange("PSHOST", "stderr");
        _output.Add(new InformationOutput(record));
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
            _output.Add(new StringOutput(line));
        }

        var rest = await sr.ReadToEndAsync();
        if (!string.IsNullOrEmpty(rest))
        {
            _output.Add(new StringOutput(rest));
        }
        PrintDebug("Complete stringReader");
        _output.CompleteAdding();
    }

    protected override void BeginProcessing()
    {
        _encoding = System.Text.Encoding.GetEncoding(Encoding);
        _stderrDecoder = _encoding.GetDecoder();

        var cmdInfo = GetCommandAndArguments();
        _processRunner = new RawProcessRunner(cmdInfo.Path, cmdInfo.Arguments);

        if (AsString)
        {
            _stringServer = new(PipeDirection.Out, HandleInheritability.None);
            _stringClient = new(PipeDirection.In, _stringServer.ClientSafePipeHandle);
            if (Output.HasFlag(OutputType.Stdout))
            {
                _processRunner.OnStdout += OnOutputChunkAsString;
            }
            if (Output.HasFlag(OutputType.Stderr))
            {
                _processRunner.OnStderr += OnOutputChunkAsString;
            }
            else
            {
                _processRunner.OnStderr += OnErrorChunk;
            }
            WriteVerboseRaw($"Set encoding: {_encoding.WebName}");
            _stringReaderTask = AsyncDecode(_stringClient, _encoding);
        }
        else
        {
            if (Output.HasFlag(OutputType.Stdout))
            {
                _processRunner.OnStdout += OnOutputChunk;
            }
            if (Output.HasFlag(OutputType.Stderr))
            {
                _processRunner.OnStderr += OnOutputChunk;
            }
            else
            {
                _processRunner.OnStderr += OnErrorChunk;
            }
        }
        _processRunner.Start(PipelineStopToken);
        WriteVerboseProcess($"Started process with arguments: [{string.Join(", ", _processRunner.Arguments)}] ({_processRunner.StartTime.ToLocalTime():HH:mm:ss.fff})");
    }

    protected override void ProcessRecord()
    {
        if (InputBytes is not null)
        {
            _totalReadBytes += InputBytes.Length;
            _readCount++;
            PrintDebug($"Read {InputBytes.Length} bytes from pipeline");
            _ = _processRunner.WriteStdinAsync(InputBytes, PipelineStopToken);
        }
    }

    protected override void StopProcessing()
    {
        WriteVerboseProcess("Stopping process");
        _processRunner.Kill();
        _stringServer?.Dispose();
        _stringClient?.Dispose();

        PrintDebugMessages();
    }

    protected override void EndProcessing()
    {
        if (_totalReadBytes > 0)
        {
            WriteVerboseProcess($"Read total: {_totalReadBytes}, count: {_readCount}");
        }
        _processRunner.CloseStdin();

        Task<int> exitTask;

        if (AsString)
        {
            exitTask = Task.Run(async () =>
            {
                PrintDebug($"Wait process runner's output to finish");
                var exitCode = await _processRunner.WaitForCompleteAsync(PipelineStopToken);
                _stringServer?.Close();
                if (_stringReaderTask is not null)
                    await _stringReaderTask;
                return exitCode;
            });

            int lineCount = 0;
            foreach (var output in _output.GetConsumingEnumerable(PipelineStopToken))
            {
                switch (output)
                {
                    case StringOutput line:
                        lineCount++;
                        PrintDebug($"Output line: [{lineCount}] {line.Value}");
                        WriteObject(line.Value);
                        break;
                    case InformationOutput info:
                        PrintDebug($"Output Information: {info.Value}");
                        WriteInformation(info.Value);
                        break;
                }
            }

            if (lineCount > 0)
            {
                WriteVerboseRaw($"Output total line: {lineCount}");
            }
        }
        else
        {
            exitTask = Task.Run(async () =>
            {
                PrintDebug($"Wait process runner's output to finish");
                var exitCode = await _processRunner.WaitForCompleteAsync(PipelineStopToken);
                PrintDebug("Complete queueInput");
                _output.CompleteAdding();
                return exitCode;
            });

            long totalWriteBytes = 0;
            int writeCount = 0;
            foreach (var output in _output.GetConsumingEnumerable(PipelineStopToken))
            {
                switch (output)
                {
                    case ChunkOutput chunk:
                        totalWriteBytes += chunk.Value.Length;
                        writeCount++;
                        PrintDebug($"Output chunk: {chunk.Value.Length} bytes");
                        WriteObject(chunk.Value, false);
                        break;
                    case InformationOutput info:
                        PrintDebug($"Output Information: {info.Value}");
                        WriteInformation(info.Value);
                        break;
                }
            }

            if (totalWriteBytes > 0)
            {
                WriteVerboseProcess($"Output total: {totalWriteBytes}, count: {writeCount}");
            }
        }

        try
        {
            var exitCode = exitTask.GetAwaiter().GetResult();
            WriteVerboseProcess($"End [ExitCode = {exitCode}] ({_processRunner.ExitTime.ToLocalTime():HH:mm:ss.fff}, Duration={_processRunner.ExitTime - _processRunner.StartTime}))");
            SessionState.PSVariable.Set("LASTEXITCODE", exitCode);

        }
        catch (Exception ex)
        {
            SessionState.PSVariable.Set("LASTEXITCODE", 1);
            ThrowTerminatingError(new ErrorRecord(ex,
                                                  "RawCommandProcessCompletionFailed",
                                                  ErrorCategory.OperationStopped,
                                                  this));
        }
        finally
        {
            PrintDebugMessages();
        }
    }

    private void WriteVerboseProcess(ReadOnlySpan<char> message)
        => WriteVerboseRaw($"[{_processRunner.Pid}][{_processRunner.Name}] {message}");

    private (string Path, IEnumerable<string> Arguments) GetCommandAndArguments()
    {
        if (Script is not null)
        {
            var ast = Script.Ast as ScriptBlockAst;
            var cmdAst = ast?.EndBlock.Statements.OfType<PipelineAst>()
                                                 .SelectMany(stmt => stmt.PipelineElements)
                                                 .FirstOrDefault(cmdAst => cmdAst is CommandAst) as CommandAst
                         ?? throw new InvalidOperationException("raw: no command specified");

            var arguments = cmdAst.CommandElements.Skip(1)
                                                  .Select(elem => elem switch
                                                  {
                                                      StringConstantExpressionAst str => str.Value,
                                                      ExpandableStringExpressionAst exp => exp.Value,
                                                      _ => elem.Extent.Text
                                                  });
            return (GetAppInfo(cmdAst.GetCommandName()).Path, arguments);
        }

        if (!string.IsNullOrWhiteSpace(Command))
        {
            return (GetAppInfo(Command).Path, Arguments);
        }

        ThrowTerminatingError(new(new ArgumentException("Either -Script or -Command must be provided."),
                                  "MissingParameter",
                                  ErrorCategory.InvalidArgument,
                                  this));
        return (string.Empty, []);

        ApplicationInfo GetAppInfo(string name)
            => InvokeCommand.GetCommand(name, CommandTypes.Application) as ApplicationInfo
                ?? throw new InvalidOperationException($"raw: command '{name}' not found");
    }

    [Conditional("DEBUG")]
    private void PrintDebugMessages()
    {
#if DEBUG
        foreach (var msg in _processRunner.DebugMsgs)
        {
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.Error.WriteLine($"({msg.TimeSpan}){msg.Source,-25} {msg.Category,10}: {msg.Message}");
        }
        Console.ResetColor();
#endif
    }

    [Conditional("DEBUG")]
    protected new void PrintDebug(string msg,
                                  ConsoleColor fg = ConsoleColor.DarkGray,
                                  [CallerMemberName] string callerMethodName = "",
                                  [CallerLineNumber] int callerLineNumber = 0)
    {
        _processRunner?.Log($"[{MyInvocation.MyCommand.Name}] {msg}", "cmdlet", callerMethodName, callerLineNumber);
    }
}
