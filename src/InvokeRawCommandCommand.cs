using System.Collections.Concurrent;
using System.IO.Pipes;
using System.Management.Automation;
using System.Management.Automation.Language;
using System.Text;

namespace Sashimi;

[Flags]
public enum OutputType
{
    Stdout = 1,
    Stderr = 2,
    All = Stdout | Stderr
}

[Cmdlet(VerbsLifecycle.Invoke, "RawCommand", DefaultParameterSetName = NormalParameterSet)]
[Alias("raw")]
[OutputType(typeof(byte[]), ParameterSetName = [NormalParameterSet, ScriptBlockParameterSet])]
[OutputType(typeof(string), ParameterSetName = [NormalAsStringParameterSet, ScriptBlockAsStringParameterSet])]
public class InvokeRawCommandCommand : RawCommandBase
{
    private const string NormalParameterSet = "Normal";
    private const string ScriptBlockParameterSet = "ScriptBlock";
    private const string NormalAsStringParameterSet = "NormalAsString";
    private const string ScriptBlockAsStringParameterSet = "ScriptBlockAsString";

    [Parameter(ParameterSetName = NormalParameterSet, Mandatory = true, Position = 0)]
    [Parameter(ParameterSetName = NormalAsStringParameterSet, Mandatory = true, Position = 0)]
    public string Command { get; set; } = null!;

    [Parameter(ParameterSetName = NormalParameterSet, ValueFromRemainingArguments = true, Position = 1)]
    [Parameter(ParameterSetName = NormalAsStringParameterSet, ValueFromRemainingArguments = true, Position = 1)]
    public string[] Arguments { get; set; } = [];

    [Parameter(ParameterSetName = ScriptBlockParameterSet, Mandatory = true, Position = 0)]
    [Parameter(ParameterSetName = ScriptBlockAsStringParameterSet, Mandatory = true, Position = 0)]
    public ScriptBlock Script { get; set; } = null!;

    [Parameter(ValueFromPipeline = true)]
    public byte[]? InputBytes { get; set; }

    [Parameter()]
    [Alias("o")]
    public OutputType Output { get; set; } = OutputType.Stdout;

    [Parameter(ParameterSetName = NormalAsStringParameterSet, Mandatory = true)]
    [Parameter(ParameterSetName = ScriptBlockAsStringParameterSet, Mandatory = true)]
    [Alias("s")]
    public SwitchParameter AsString { get; set; }

    private RawProcessRunner _processRunner = null!;

    private readonly ConcurrentQueue<byte[]> _stdoutQueue = [];
    private readonly AutoResetEvent _stdoutEvent = new(false);
    private volatile bool _queueInputCompleted = false;

    private long _totalReadBytes;
    private int _readCount;

    private AnonymousPipeServerStream? _stringServer;
    private AnonymousPipeClientStream? _stringClient;
    private Task? _stringReaderTask;
    private readonly ConcurrentQueue<string> _stringQueue = [];
    private readonly AutoResetEvent _stringQueueEvent = new(false);
    private volatile bool _readerCompleted = false;

    private void OnOutputChunk(byte[] chunk)
    {
        _stdoutQueue.Enqueue(chunk);
        _stdoutEvent.Set();
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

    private async Task AsyncDecode(Stream stream, Encoding encoding)
    {
        using var sr = new StreamReader(stream, encoding);
        while (true)
        {
            string? line = await sr.ReadLineAsync();
            if (line is null)
                break;

            _stringQueue.Enqueue(line);
            PrintDebug("Set stringQueueEvent");
            _stringQueueEvent.Set();
        }

        var rest = await sr.ReadToEndAsync();
        if (!string.IsNullOrEmpty(rest))
        {
            _stringQueue.Enqueue(rest);
        }
        PrintDebug("Complete stringReader");
        _readerCompleted = true;
        PrintDebug("Set stringQueueEvent (final)");
        _stringQueueEvent.Set();
    }

    protected override void BeginProcessing()
    {
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
            WriteVerboseRaw("Set encoding: UTF-8");
            _stringReaderTask = AsyncDecode(_stringClient, Encoding.UTF8);
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
        }
        _processRunner.StartAsync().Wait();
        WriteVerboseProcess($"Started process with arguments: [{string.Join(", ", _processRunner.Arguments)}]");
    }

    protected override void ProcessRecord()
    {
        if (InputBytes is not null)
        {
            _totalReadBytes += InputBytes.Length;
            _readCount++;
            PrintDebug($"Read {InputBytes.Length} bytes from pipeline");
            _ = _processRunner.WriteStdinAsync(InputBytes);
        }
    }

    protected override void StopProcessing()
    {
        WriteVerboseProcess("Stopping process");
        _processRunner.Kill();
        _stringServer?.Dispose();
        _stringClient?.Dispose();
    }

    protected override void EndProcessing()
    {
        if (_totalReadBytes > 0)
        {
            WriteVerboseProcess($"Read total: {_totalReadBytes}, count: {_readCount}");
        }
        _processRunner.CloseStdin();

        var exitTask = _processRunner.WaitForExitAsync();
        var outputTask = Task.Run(() =>
        {
            PrintDebug($"Wait process runner's output to finish");
            _processRunner.WaitOutput();
            _stringServer?.Close();
            PrintDebug("Complete queueInput");
            _queueInputCompleted = true;
            PrintDebug("Set stdoutEvent (final)");
            _stdoutEvent.Set();
        });

        if (AsString)
        {
            int lineCount = 0;
            while (!_readerCompleted || !_stringQueue.IsEmpty)
            {
                while (_stringQueue.TryDequeue(out var line))
                {
                    lineCount++;
                    PrintDebug($"Output line: [{lineCount}] {line}");
                    WriteObject(line);
                }

                if (!_readerCompleted)
                {
                    PrintDebug("Wait for stringQueueEvent");
                    _stringQueueEvent.WaitOne();
                }
            }

            if (lineCount > 0)
            {
                WriteVerboseRaw($"Output total line: {lineCount}");
            }
        }
        else
        {
            long totalWriteBytes = 0;
            int writeCount = 0;
            while (!_queueInputCompleted || !_stdoutQueue.IsEmpty)
            {
                while (_stdoutQueue.TryDequeue(out var chunk))
                {
                    totalWriteBytes += chunk.Length;
                    writeCount++;
                    PrintDebug($"Output chunk: {chunk.Length} bytes");
                    WriteObject(chunk, false);
                }
                if (!_queueInputCompleted)
                {
                    PrintDebug("Wait for stdoutEvent");
                    _stdoutEvent.WaitOne();
                }
            }

            if (totalWriteBytes > 0)
            {
                WriteVerboseProcess($"Output total: {totalWriteBytes}, count: {writeCount}");
            }
        }

        try
        {
            _stringReaderTask?.Wait();
            outputTask.Wait();
            exitTask.Wait();
        }
        catch { }
        var exitCode = exitTask.Result;

        WriteVerboseProcess($"End [ExitCode = {exitCode}]");
        SessionState.PSVariable.Set("LASTEXITCODE", exitCode);
    }

    private void WriteVerboseProcess(ReadOnlySpan<char> message)
        => WriteVerbose($"[{_processRunner.Pid}][{_processRunner.Name}] {message}");

    private (string Path, IEnumerable<string> Arguments) GetCommandAndArguments()
    {
        if (ParameterSetName is ScriptBlockParameterSet)
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
        else
        {
            return (GetAppInfo(Command).Path, Arguments);
        }

        ApplicationInfo GetAppInfo(string name)
            => InvokeCommand.GetCommand(name, CommandTypes.Application) as ApplicationInfo
                ?? throw new InvalidOperationException($"raw: command '{name}' not found");
    }
}
