using System.Management.Automation;
using System.Text;

namespace Sashimi;

[Cmdlet(VerbsData.ConvertFrom, "RawString")]
[OutputType(typeof(byte[]))]
[Alias("a2b")]
public sealed class ConvertFromRawStringComand : RawCommandBase
{
    [Parameter(Mandatory = true, ValueFromPipeline = true, Position = 0)]
    public string InputString { get; set; } = null!;

    [Parameter()]
    [Alias("e")]
    public string Encoding { get; set; } = "utf-8";

    private Encoding _encoding = null!;

    private long _totalWriteBytes;
    private int _writeCount;

    protected override void BeginProcessing()
    {
        _encoding = System.Text.Encoding.GetEncoding(Encoding);
        WriteVerboseRaw($"Set encoding: {_encoding.BodyName} [{_encoding.EncodingName}]");
    }

    protected override void ProcessRecord()
    {
        var bytes = _encoding.GetBytes(InputString);
        _totalWriteBytes += bytes.Length;
        _writeCount++;
        PrintDebug($"Output chunk: {bytes.Length} bytes");
        WriteObject(bytes, false);
    }

    protected override void EndProcessing()
    {
        WriteVerboseRaw($"Output total: {_totalWriteBytes}, count: {_writeCount}");
    }
}
