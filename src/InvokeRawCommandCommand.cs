using System.Diagnostics.CodeAnalysis;
using System.Management.Automation;
using System.Management.Automation.Language;
using Sashimi.Internal;

namespace Sashimi;

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
    public OutputFrom Output { get; set; } = OutputFrom.Stdout;

    [Parameter(HelpMessageBaseName = MessageBaseName, HelpMessageResourceId = "InvokeRawCommand.parameters.AsString")]
    [Alias("s")]
    public SwitchParameter AsString { get; set; }

    [Parameter(HelpMessageBaseName = MessageBaseName, HelpMessageResourceId = "InvokeRawCommand.parameters.Encoding")]
    [ArgumentCompleter(typeof(EncodingCompleter))]
    [Alias("e")]
    public string Encoding { get; set; } = "UTF-8";

    private ExecutionEngine? _engine;

    protected override void BeginProcessing()
    {
        try
        {
            if (Script is not null)
            {
                if (SniffScriptBlock(Script, out var commandAst))
                {
                    var appInfo = GetAppInfo(commandAst.GetCommandName());
                    Arguments = GetArguments(commandAst).ToArray();
                    _engine = new RawExecutionEngine(this, appInfo.Path);
                }
                else
                {
                    ThrowTerminatingError(new(new NotSupportedException("The ScriptBlock contains multiple statements or pipeline elements. "
                                                                        + "Currently only a single external command is supported: raw { cmd }."),
                                              "NotSupported",
                                              ErrorCategory.NotImplemented,
                                              this));
                    return;
                }
            }
            else if (Command is not null)
            {
                _engine = new RawExecutionEngine(this, GetAppInfo(Command).Path);
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
            ThrowTerminatingError(new(ex, "FailedToInitializeExecutionEngine", ErrorCategory.InvalidArgument, this));
            return;
        }

        _engine.BeginProcessing();
    }

    protected override void ProcessRecord()
    {
        if (InputBytes is not null)
        {
            _engine?.ProcessRecord(InputBytes);
        }
    }

    protected override void StopProcessing()
    {
        _engine?.StopProcessing();
    }

    protected override void EndProcessing()
    {
        try
        {
            _engine?.EndProcessing();
        }
        catch(Exception ex)
        {
            ThrowTerminatingError(new ErrorRecord(ex,
                                                  "RawCommandProcessCompletionFailed",
                                                  ErrorCategory.OperationStopped,
                                                  this));
        }
    }

    private static bool SniffScriptBlock(ScriptBlock scriptBlock, [MaybeNullWhen(false)] out CommandAst commandAst)
    {
        commandAst = null;
        var ast = scriptBlock.Ast as ScriptBlockAst;
        if (ast is null)
            return false;

        if (ast.BeginBlock?.Statements.Count > 0)
            return false;

        if (ast.ProcessBlock?.Statements.Count > 0)
            return false;

        if (ast.EndBlock.Statements.Count != 1)
            return false;

        var pipeAst = ast.EndBlock.Statements[0] as PipelineAst;
        if (pipeAst is null)
            return false;

        if (pipeAst.PipelineElements.Count != 1)
            return false;

        commandAst = pipeAst.PipelineElements[0] as CommandAst;

        return commandAst is not null;
    }

    private static IEnumerable<string> GetArguments(CommandAst commandAst)
        => commandAst.CommandElements.Skip(1)
                                     .Select(elem => elem switch
                                      {
                                          StringConstantExpressionAst str => str.Value,
                                          ExpandableStringExpressionAst exp => exp.Value,
                                          _ => elem.Extent.Text
                                      });

    private ApplicationInfo GetAppInfo(string name)
            => InvokeCommand.GetCommand(name, CommandTypes.Application) as ApplicationInfo
               ?? throw new InvalidOperationException($"raw: command '{name}' not found");
}
