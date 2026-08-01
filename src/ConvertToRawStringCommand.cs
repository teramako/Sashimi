using System.Management.Automation;
using Sashimi.Internal;

namespace Sashimi;

[Cmdlet(VerbsData.ConvertTo, "RawString")]
[OutputType(typeof(string))]
[Alias("b2a")]
public sealed class ConvertToRawStringCommand : RawCommandBase
{
    [Parameter(Mandatory = true, ValueFromPipeline = true, Position = 0,
               HelpMessageBaseName = MessageBaseName, HelpMessageResourceId = "ConvertToRawString.parameters.InputBytes")]
    public byte[] InputBytes { get; set; } = null!;

    [Parameter(HelpMessageBaseName = MessageBaseName, HelpMessageResourceId = "ConvertToRawString.parameters.Encoding")]
    [ArgumentCompleter(typeof(EncodingCompleter))]
    [Alias("e")]
    public string Encoding { get; set; } = "utf-8";

    [Parameter(HelpMessageBaseName = MessageBaseName, HelpMessageResourceId = "ConvertToRawString.parameters.Raw")]
    [Alias("r")]
    public SwitchParameter Raw { get; set; }

    private long _totalReadBytes;
    private int _readCount;
    private int _lineCount;

    private ChunkStringDecoder _stringDecoder = null!;

    private void Output(ReadOnlySpan<char> line)
    {
        WriteObject(line.ToString(), false);
        if (!Raw)
        {
            _lineCount++;
        }
    }

    protected override void BeginProcessing()
    {
        try
        {
            var encoding = EncodingCompleter.GetEncoding(Encoding);
            _stringDecoder = new(encoding, Output, Raw.ToBool());
            WriteVerboseRaw($"Set encoding: {encoding.WebName} [{encoding.EncodingName}]");
        }
        catch(Exception ex)
        {
            ThrowTerminatingError(new(ex, "InvalidEncoding", ErrorCategory.InvalidArgument, this));
        }
    }

    protected override void ProcessRecord()
    {
        if (InputBytes.Length > 0)
        {
            _totalReadBytes += InputBytes.Length;
            _readCount++;
            _stringDecoder.Decode(InputBytes);
        }
    }

    protected override void EndProcessing()
    {
        _stringDecoder.EmitRemaining();

        if (_totalReadBytes > 0)
        {
            WriteVerboseRaw($"Read total: {_totalReadBytes}, count: {_readCount}");
        }

        if (_lineCount > 0)
        {
            WriteVerboseRaw($"Output total line: {_lineCount}");
        }
    }
}
