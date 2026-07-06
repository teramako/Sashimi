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
internal record InformationOutput(InformationRecord Value) : RawOutputItem;

[Cmdlet(VerbsLifecycle.Invoke, "RawCommand", DefaultParameterSetName = NormalParameterSet)]
[Alias("raw")]
[OutputType(typeof(byte[]))]
[OutputType(typeof(string))]
public class InvokeRawCommandCommand : RawCommandBase
{
    private const string NormalParameterSet = "Normal";
    private const string ScriptBlockParameterSet = "ScriptBlock";

    [Parameter(ParameterSetName = NormalParameterSet, Mandatory = true, Position = 0,
               HelpMessageBaseName = MessageBaseName, HelpMessageResourceId = "InvokeRawCommand.parameters.Command")]
    [ArgumentCompleter(typeof(NativeCommandCompleter))]
    public string? Command { get; set; }

    [Parameter(ParameterSetName = NormalParameterSet, ValueFromRemainingArguments = true, Position = 1,
               HelpMessageBaseName = MessageBaseName, HelpMessageResourceId = "InvokeRawCommand.parameters.Arguments")]
    public string[] Arguments { get; set; } = [];

    [Parameter(ParameterSetName = ScriptBlockParameterSet, Mandatory = true, Position = 0,
               HelpMessageBaseName = MessageBaseName, HelpMessageResourceId = "InvokeRawCommand.parameters.Script")]
    public ScriptBlock? Script { get; set; }

    [Parameter(ValueFromPipeline = true,
               HelpMessageBaseName = MessageBaseName, HelpMessageResourceId = "InvokeRawCommand.parameters.InputBytes")]
    public byte[]? InputBytes { get; set; }

    [Parameter(HelpMessageBaseName = MessageBaseName, HelpMessageResourceId = "InvokeRawCommand.parameters.Output")]
    [Alias("o")]
    public OutputType Output { get; set; } = OutputType.Stdout;

    [Parameter(HelpMessageBaseName = MessageBaseName, HelpMessageResourceId = "InvokeRawCommand.parameters.AsString")]
    [Alias("s")]
    public SwitchParameter AsString { get; set; }

    [Parameter(HelpMessageBaseName = MessageBaseName, HelpMessageResourceId = "InvokeRawCommand.parameters.Encoding")]
    [ArgumentCompleter(typeof(EncodingCompleter))]
    [Alias("e")]
    public string Encoding { get; set; } = "UTF-8";

    private RawExecutionEngine? _engine;

    protected override void BeginProcessing()
    {
        Encoding encoding;
        ApplicationInfo appInfo;
        try
        {
            encoding = EncodingCompleter.GetEncoding(Encoding);
        }
        catch (Exception ex)
        {
            ThrowTerminatingError(new(ex, "InvalidEncoding", ErrorCategory.InvalidArgument, this));
            return;
        }

        try
        {
            if (Script is not null)
            {
                (appInfo, Arguments) = GetCommandAndArguments(Script);
            }
            else if (Command is not null)
            {
                appInfo = GetAppInfo(Command);
            }
            else
            {
                ThrowTerminatingError(new(new ArgumentException("Either -Script or -Command must be provided."),
                                          "MissingParameter",
                                          ErrorCategory.InvalidArgument,
                                          this));
                return;
            }
        }
        catch (Exception ex)
        {
            ThrowTerminatingError(new(ex, "CommandNotFound", ErrorCategory.InvalidArgument, this));
            return;
        }

        _engine = new RawExecutionEngine(appInfo.Path, Arguments, encoding, AsString, Output);
        _engine.StartAsync(PipelineStopToken);
        WriteVerboseRaw($"{_engine.LogPrefix} Started process with arguments: [{string.Join(", ", Arguments)}] ({_engine.StartTime.ToLocalTime():HH:mm:ss.fff})");
    }

    protected override void ProcessRecord()
    {
        if (_engine is not null && InputBytes is not null)
        {
            _engine.WriteInputAsync(InputBytes, PipelineStopToken).Wait();
        }
    }

    protected override void StopProcessing()
    {
        if (_engine is not null)
        {
            WriteVerboseRaw($"{_engine.LogPrefix} Stopping process");
            _engine.Kill();

            _engine.PrintDebugMessages();
        }
    }

    protected override void EndProcessing()
    {
        if (_engine is not null)
        {
            WaitForExecute();
        }
    }

    private void WaitForExecute()
    {
        if (_engine is null)
            return;

        var exitTask = _engine.WaitForExitAsync(PipelineStopToken);
        long totalWriteBytes = 0;
        int writeCount = 0;
        int lineCount = 0;
        foreach (var output in _engine.Output.GetConsumingEnumerable(PipelineStopToken))
        {
            switch (output)
            {
                case StringOutput line:
                    lineCount++;
                    _engine.PrintDebug($"[{MyCommandName}] Output line: [{lineCount}] {line.Value}");
                    WriteObject(line.Value);
                    break;
                case ChunkOutput chunk:
                    totalWriteBytes += chunk.Value.Length;
                    writeCount++;
                    _engine.PrintDebug($"[{MyCommandName}] Output chunk: {chunk.Value.Length} bytes");
                    WriteObject(chunk.Value, false);
                    break;
                case InformationOutput info:
                    _engine.PrintDebug($"[{MyCommandName}] Output Information: {info.Value}");
                    WriteInformation(info.Value);
                    break;
            }
        }
        if (lineCount > 0)
        {
            WriteVerboseRaw($"{_engine.LogPrefix} Output total line: {lineCount}");
        }
        if (totalWriteBytes > 0)
        {
            WriteVerboseRaw($"{_engine.LogPrefix} Output total: {totalWriteBytes}, count: {writeCount}");
        }

        try
        {
            var exitCode = exitTask.GetAwaiter().GetResult();
            WriteVerboseRaw($"{_engine.LogPrefix} End [ExitCode = {exitCode}]"
                            + $" ({_engine.ExitTime.ToLocalTime():HH:mm:ss.fff},"
                            + $" Duration={_engine.ExitTime - _engine.StartTime}))");
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
            _engine.PrintDebugMessages();
        }
    }

    private (ApplicationInfo AppInfo, string[] Arguments) GetCommandAndArguments(ScriptBlock scriptBlock)
    {
        var ast = scriptBlock.Ast as ScriptBlockAst;
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
        return (GetAppInfo(cmdAst.GetCommandName()), arguments.ToArray());
    }

    private ApplicationInfo GetAppInfo(string name)
            => InvokeCommand.GetCommand(name, CommandTypes.Application) as ApplicationInfo
               ?? throw new InvalidOperationException($"raw: command '{name}' not found");
}
