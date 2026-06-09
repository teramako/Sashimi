using System.Collections.Concurrent;
using System.Diagnostics;
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
    private volatile bool _stdoutCompleted = false;

    private void OnOutputChunk(byte[] chunk)
    {
        _stdoutQueue.Enqueue(chunk);
        _stdoutEvent.Set();
    }

    protected override void BeginProcessing()
    {
        var psi = BuildProcessStartInfo();
        WriteVerbose($"[{psi.FileName}] Start with arguments: [{string.Join(", ", psi.ArgumentList)}]");
        _processRunner = new RawProcessRunner(psi);
        if (Output.HasFlag(OutputType.Stdout))
        {
            _processRunner.OnStdout += OnOutputChunk;
        }
        if (Output.HasFlag(OutputType.Stderr))
        {
            _processRunner.OnStderr += OnOutputChunk;
        }
        _ = _processRunner.StartAsync();
    }

    protected override void ProcessRecord()
    {
        if (InputBytes is not null)
        {
            _ = _processRunner.WriteStdinAsync(InputBytes);
        }
    }

    protected override void EndProcessing()
    {
        _processRunner.CloseStdin();

        var task = Task.Run(async () =>
        {
            var exitCode = await _processRunner.WaitForExitAsync();
            _stdoutCompleted = true;
            _stdoutEvent.Set();
            return exitCode;
        });

        while (!_stdoutCompleted || !_stdoutQueue.IsEmpty)
        {
            while (_stdoutQueue.TryDequeue(out var chunk))
            {
                WriteObject(chunk, false);
            }
            if (!_stdoutCompleted)
                _stdoutEvent.WaitOne();
        }

        var exitCode = task.Result;

        WriteVerbose($"[{_processRunner.Name}] End [ExitCode = {exitCode}]");
        SessionState.PSVariable.Set("LASTEXITCODE", exitCode);
    }

    private ProcessStartInfo BuildProcessStartInfo()
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
            return RawProcessRunner.CreateProcessStartInfo(GetAppInfo(cmdAst.GetCommandName()).Path, arguments);
        }
        else
        {
            return RawProcessRunner.CreateProcessStartInfo(GetAppInfo(Command).Path, Arguments);
        }

        ApplicationInfo GetAppInfo(string name)
            => InvokeCommand.GetCommand(name, CommandTypes.Application) as ApplicationInfo
                ?? throw new InvalidOperationException($"raw: command '{name}' not found");
    }
}
