using System.Management.Automation;

namespace Sashimi.Internal;

internal abstract class ExecutionEngine(RawCommandBase cmdlet)
{
    protected readonly RawCommandBase Cmdlet = cmdlet;

    /// <inheritdoc cref="Cmdlet.PipelineStopToken"/>
    protected CancellationToken PipelineStopToken => Cmdlet.PipelineStopToken;

    protected void WriteVerboseRaw(ReadOnlySpan<char> message)
        => Cmdlet.WriteVerboseRaw(message);

    protected void WriteInformation(InformationRecord informationRecord)
        => Cmdlet.WriteInformation(informationRecord);

    /// <inheritdoc cref="Cmdlet.WriteObject(object, bool)"/>
    protected void WriteObject(object sendToPipeline, bool enumerateCollection = false)
        => Cmdlet.WriteObject(sendToPipeline, enumerateCollection);

    /// <inheritdoc cref="Cmdlet.BeginProcessing"/>
    public abstract void BeginProcessing();

    /// <inheritdoc cref="Cmdlet.ProcessRecord"/>
    public virtual void ProcessRecord(byte[] inputBytes)
    {
    }

    /// <inheritdoc cref="Cmdlet.ProcessRecord"/>
    public virtual void ProcessRecord(string inputString)
    {
    }

    /// <inheritdoc cref="Cmdlet.StopProcessing"/>
    public virtual void StopProcessing()
    {
    }

    /// <inheritdoc cref="Cmdlet.EndProcessing"/>
    public abstract void EndProcessing();
}
