using System.Management.Automation;
using Sashimi.Internal;

namespace Sashimi;

[Cmdlet(VerbsDiagnostic.Test, "RawCommand")]
[Alias("raw?")]
[OutputType(typeof(bool))]
public class TestRawCommandCommand : RawCommandBase
{
    [Parameter(Mandatory = true, Position = 0,
               HelpMessageBaseName = MessageBaseName, HelpMessageResourceId = "InvokeRawCommand.parameters.Command")]
    [ArgumentCompleter(typeof(ExternalCommandCompleter))]
    public string Command { get; set; } = string.Empty;

    [Parameter(ValueFromRemainingArguments = true, Position = 1,
               HelpMessageBaseName = MessageBaseName, HelpMessageResourceId = "InvokeRawCommand.parameters.Arguments")]
    public string[] Arguments { get; set; } = [];

    [Parameter(ValueFromPipeline = true,
               HelpMessageBaseName = MessageBaseName, HelpMessageResourceId = "InvokeRawCommand.parameters.Input")]
    public object? Input { get; set; }

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
            _engine = new TestRawExecutionEngine(this,
                                                 GetAppInfo(Command).Path,
                                                 Arguments,
                                                 EncodingCompleter.GetEncoding(Encoding),
                                                 AsString);
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
