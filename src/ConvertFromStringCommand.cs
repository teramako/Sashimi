using System.Management.Automation;
using System.Text;

[Cmdlet(VerbsData.ConvertFrom, "String")]
[OutputType(typeof(byte[]))]
[Alias("a2b")]
public sealed class ConvertFromStringComand : Cmdlet
{
    [Parameter(Mandatory = true, ValueFromPipeline = true, Position = 0)]
    public string InputString { get; set; } = null!;

    [Parameter()]
    [Alias("e")]
    public string Encoding { get; set; } = "utf-8";

    private Encoding _encoding = null!;

    protected override void BeginProcessing()
    {
        _encoding = System.Text.Encoding.GetEncoding(Encoding);
        WriteVerbose($"[ConvertFrom-String] Set encoding: {_encoding.BodyName} [{_encoding.EncodingName}]");
    }

    protected override void ProcessRecord()
    {
        var bytes = _encoding.GetBytes(InputString);
        WriteVerbose($"[ConvertFrom-String] Output {bytes.Length} bytes");
        WriteObject(bytes, false);
    }
}
