using System.Management.Automation;
using System.Text;

namespace Sashimi;

[Cmdlet(VerbsData.ConvertFrom, "RawString")]
[OutputType(typeof(byte[]))]
[Alias("a2b")]
public sealed class ConvertFromRawStringComand : RawCommandBase
{
    [Parameter(Mandatory = true, ValueFromPipeline = true, Position = 0,
               HelpMessageBaseName = MessageBaseName, HelpMessageResourceId = "ConvertFromRawString.parameters.InputString")]
    [AllowEmptyString]
    public string InputString { get; set; } = null!;

    [Parameter(HelpMessageBaseName = MessageBaseName, HelpMessageResourceId = "ConvertFromRawString.parameters.Encoding")]
    [ArgumentCompleter(typeof(EncodingCompleter))]
    [Alias("e")]
    public string Encoding { get; set; } = "utf-8";

    [Parameter(HelpMessageBaseName = MessageBaseName, HelpMessageResourceId = "ConvertFromRawString.parameters.Delimiter")]
    [AllowEmptyString]
    [AllowNull]
    [Alias("d")]
    [ArgumentCompletions("`n", "`r`n", "`r")]
    public string? Delimiter { get; set; }

    private Encoding _encoding = null!;

    private long _totalWriteBytes;
    private int _writeCount;

    protected override void BeginProcessing()
    {
        try
        {
            _encoding = EncodingCompleter.GetEncoding(Encoding);
        }
        catch(Exception ex)
        {
            ThrowTerminatingError(new(ex, "InvalidEncoding", ErrorCategory.InvalidArgument, this));
        }
        WriteVerboseRaw($"Set encoding: {_encoding.WebName} [{_encoding.EncodingName}]");
    }

    protected override void ProcessRecord()
    {
        if (_writeCount > 0 && !string.IsNullOrEmpty(Delimiter))
        {
            WriteBytes(Delimiter);
        }
        WriteBytes(InputString);
        _writeCount++;
    }

    private void WriteBytes(string text)
    {
        var bytes = _encoding.GetBytes(text);
        _totalWriteBytes += bytes.Length;
        PrintDebug($"Output chunk: {bytes.Length} bytes");
        WriteObject(bytes, false);
    }

    protected override void EndProcessing()
    {
        WriteVerboseRaw($"Output total: {_totalWriteBytes}, count: {_writeCount}");
    }
}
