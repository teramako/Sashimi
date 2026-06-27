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
    Both = Stdout | Stderr
}

internal abstract record RawOutputItem;
internal record ChunkOutput(byte[] Value) : RawOutputItem;
internal record StringOutput(string Value) : RawOutputItem;

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
        Task[] tasks;

        if (AsString)
        {
            tasks = [
                _stringReaderTask!,
                Task.Run(() =>
                {
                    PrintDebug($"Wait process runner's output to finish");
                    _processRunner.WaitOutput();
                    _stringServer?.Close();
                }),
                exitTask,
            ];

            int lineCount = 0;
            foreach (StringOutput line in _output.GetConsumingEnumerable())
            {
                lineCount++;
                PrintDebug($"Output line: [{lineCount}] {line.Value}");
                WriteObject(line.Value);
            }

            if (lineCount > 0)
            {
                WriteVerboseRaw($"Output total line: {lineCount}");
            }
        }
        else
        {
            tasks = [
                Task.Run(() =>
                {
                    PrintDebug($"Wait process runner's output to finish");
                    _processRunner.WaitOutput();
                    PrintDebug("Complete queueInput");
                    _output.CompleteAdding();
                }),
                exitTask,
            ];

            long totalWriteBytes = 0;
            int writeCount = 0;
            foreach (ChunkOutput chunk in _output.GetConsumingEnumerable())
            {
                totalWriteBytes += chunk.Value.Length;
                writeCount++;
                PrintDebug($"Output chunk: {chunk.Value.Length} bytes");
                WriteObject(chunk.Value, false);
            }

            if (totalWriteBytes > 0)
            {
                WriteVerboseProcess($"Output total: {totalWriteBytes}, count: {writeCount}");
            }
        }

        try
        {
            Task.WaitAll(tasks);
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
