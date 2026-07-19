using System.Management.Automation;
using System.Text;

namespace Sashimi.Internal;

internal sealed class TestRawExecutionEngine(RawCommandBase cmdlet,
                                             string commandPath,
                                             string[] arguments,
                                             Encoding encoding,
                                             bool asString = false)
    : RawExecutionEngine(cmdlet,
                         commandPath,
                         arguments,
                         new(RedirectTo.Information, RedirectTo.Information),
                         encoding,
                         asString)
{
    public override void EndProcessing()
    {
        var exitTask = WaitForExitAsync(PipelineStopToken);

        OutputRecords();

        try
        {
            var exitCode = exitTask.GetAwaiter().GetResult();
            WriteObject(exitCode == 0);
            Cmdlet.SessionState.PSVariable.Set("LASTEXITCODE", exitCode);
        }
        catch
        {
            WriteObject(false);
            Cmdlet.SessionState.PSVariable.Set("LASTEXITCODE", 1);
            throw;
        }
        finally
        {
            PrintDebugMessages();
        }
    }

    protected override void OutputRecords()
    {
        foreach (var output in Output.GetConsumingEnumerable(PipelineStopToken))
        {
            InformationRecord record = new(output, $"{Runner.Name} (PID: {Runner.Pid})");
            record.Tags.Add(output.From.ToString());
            Cmdlet.WriteInformation(record);
        }
    }
}
