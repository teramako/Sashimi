using System.Management.Automation;
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
    [ArgumentCompleter(typeof(ExternalCommandCompleter))]
    public string? Command { get; set; }

    [Parameter(ParameterSetName = NormalParameterSet, ValueFromRemainingArguments = true, Position = 1,
               HelpMessageBaseName = MessageBaseName, HelpMessageResourceId = "InvokeRawCommand.parameters.Arguments")]
    public string[] Arguments { get; set; } = [];

    [Parameter(ParameterSetName = ScriptBlockParameterSet, Mandatory = true, Position = 0,
               HelpMessageBaseName = MessageBaseName, HelpMessageResourceId = "InvokeRawCommand.parameters.Script")]
    public ScriptBlock? Script { get; set; }

    [Parameter(ValueFromPipeline = true,
               HelpMessageBaseName = MessageBaseName, HelpMessageResourceId = "InvokeRawCommand.parameters.Input")]
    public object? Input { get; set; }

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
                string[] forwardKeys = [nameof(Encoding), nameof(Output), nameof(AsString)];
                var forwardParams = MyInvocation.BoundParameters
                                    .Where(kv => forwardKeys.Contains(kv.Key, StringComparer.InvariantCulture))
                                    .ToDictionary();
                _engine = new ScriptBlockExecutionEngine(this, Script, forwardParams);
            }
            else if (Command is not null)
            {
                _engine = new RawExecutionEngine(this,
                                                 GetAppInfo(Command).Path,
                                                 Arguments,
                                                 Redirection.GetRedirectionFromStatement(MyInvocation.Statement, Output),
                                                 EncodingCompleter.GetEncoding(Encoding),
                                                 AsString);
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
        if (Input is null)
            return;

        if (Input is PSObject pso)
            Input = pso.BaseObject;

        if (Input is string str)
        {
            _engine?.ProcessRecord(str);
        }
        else if (LanguagePrimitives.TryConvertTo<byte[]>(Input, out var bytes))
        {
            _engine?.ProcessRecord(bytes);
        }
        else
        {
            var msg = $"Input data must be either byte[] or string: {Input?.GetType().Name ?? "null"}";
            ThrowTerminatingError(new(new InvalidDataException(msg), "InvalidDataType", ErrorCategory.InvalidType, Input));
            return;
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
}
