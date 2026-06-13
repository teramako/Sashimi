using System.Collections.Concurrent;
using System.Management.Automation;
using System.Management.Automation.Language;

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
[OutputType(typeof(byte[]))]
public class InvokeRawCommandCommand : PSCmdlet
{
    private const string NormalParameterSet = "Normal";
    private const string ScriptBlockParameterSet = "ScriptBlock";

    [Parameter(ParameterSetName = NormalParameterSet, Mandatory = true, Position = 0)]
    public string Command { get; set; } = null!;

    [Parameter(ParameterSetName = NormalParameterSet, ValueFromRemainingArguments = true, Position = 1)]
    public string[] Arguments { get; set; } = [];

    [Parameter(ParameterSetName = ScriptBlockParameterSet, Mandatory = true, Position = 0)]
    public ScriptBlock Script { get; set; } = null!;

    [Parameter(ValueFromPipeline = true)]
    public byte[]? InputBytes { get; set; }

    [Parameter()]
    public OutputType Output { get; set; } = OutputType.Stdout;

    private RawProcessRunner _processRunner = null!;

    private readonly ConcurrentQueue<byte[]> _stdoutQueue = [];
    private readonly AutoResetEvent _stdoutEvent = new(false);
    private volatile bool _queueInputCompleted = false;

    private long _totalReadBytes;
    private int _readCount;

    private void OnOutputChunk(byte[] chunk)
    {
        _stdoutQueue.Enqueue(chunk);
        _stdoutEvent.Set();
    }

    protected override void BeginProcessing()
    {
        var cmdInfo = GetCommandAndArguments();
        _processRunner = new RawProcessRunner(cmdInfo.Path, cmdInfo.Arguments);
        if (Output.HasFlag(OutputType.Stdout))
        {
            _processRunner.OnStdout += OnOutputChunk;
        }
        if (Output.HasFlag(OutputType.Stderr))
        {
            _processRunner.OnStderr += OnOutputChunk;
        }
        _processRunner.StartAsync().Wait();
        WriteVerbose($"[{_processRunner.Pid}][{_processRunner.Name}] Started process with arguments: [{string.Join(", ", _processRunner.Arguments)}]");
    }

    protected override void ProcessRecord()
    {
        if (InputBytes is not null)
        {
            _totalReadBytes += InputBytes.Length;
            _readCount++;
            WriteInformation($"[{_processRunner.Pid}][{_processRunner.Name}] Read {InputBytes.Length} bytes from pipeline", ["Sashimi.Raw.ReadChunk"]);
            _ = _processRunner.WriteStdinAsync(InputBytes);
        }
    }

    protected override void StopProcessing()
    {
        WriteVerbose($"[{_processRunner.Pid}][{_processRunner.Name}] Stopping process");
        _processRunner.Kill();
    }

    protected override void EndProcessing()
    {
        if (_totalReadBytes > 0)
        {
            WriteVerbose($"[{_processRunner.Pid}][{_processRunner.Name}] Read total: {_totalReadBytes}, count: {_readCount}");
        }
        _processRunner.CloseStdin();

        var exitTask = _processRunner.WaitForExitAsync();
        var outputTask = Task.Run(() =>
        {
            _processRunner.WaitOutput();
            _queueInputCompleted = true;
            _stdoutEvent.Set();
        });

        long totalWriteBytes = 0;
        int writeCount = 0;
        while (!_queueInputCompleted || !_stdoutQueue.IsEmpty)
        {
            while (_stdoutQueue.TryDequeue(out var chunk))
            {
                totalWriteBytes += chunk.Length;
                writeCount++;
                WriteInformation($"[{_processRunner.Pid}][{_processRunner.Name}] Output chunk: {chunk.Length} bytes", ["Sashimi.Raw.OutputChunk"]);
                WriteObject(chunk, false);
            }
            if (!_queueInputCompleted)
            {
                WriteInformation($"[{_processRunner.Pid}][{_processRunner.Name}] Wait", ["Sashimi.Raw.Wait"]);
                _stdoutEvent.WaitOne();
            }
        }

        if (totalWriteBytes > 0)
        {
            WriteVerbose($"[{_processRunner.Pid}][{_processRunner.Name}] Output total: {totalWriteBytes}, count: {writeCount}");
        }

        try
        {
            outputTask.Wait();
            exitTask.Wait();
        }
        catch { }
        var exitCode = exitTask.Result;

        WriteVerbose($"[{_processRunner.Pid}][{_processRunner.Name}] End [ExitCode = {exitCode}]");
        SessionState.PSVariable.Set("LASTEXITCODE", exitCode);
    }

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
