using System.Management.Automation;

namespace Sashimi;

[Cmdlet(VerbsData.Out, "RawFile", DefaultParameterSetName = "Default")]
[OutputType(typeof(void))]
[OutputType(typeof(byte[]), ParameterSetName = ["PassThru"])]
[Alias("bout")]
public sealed class OutRawFileCommand : RawCommandBase
{
    [Parameter(Mandatory = true, Position = 0)]
    public string Path { get; set; } = null!;

    [Parameter(Mandatory = true, ValueFromPipeline = true)]
    public byte[] InputBytes { get; set; } = null!;

    [Parameter()]
    public SwitchParameter Append { get; set; }

    [Parameter(ParameterSetName = "PassThru", Mandatory = true)]
    public SwitchParameter PassThru { get; set; }

    private FileStream? _fs;
    private long _totalWriteBytes;
    private int _writeCount;

    protected override void BeginProcessing()
    {
        var resolvedPath = GetUnresolvedProviderPathFromPSPath(Path);
        FileMode mode = Append
            ? (File.Exists(resolvedPath) ? FileMode.Append : FileMode.Create)
            : (File.Exists(resolvedPath) ? FileMode.Truncate : FileMode.CreateNew);
        WriteVerboseRaw($"Open {resolvedPath}, Mode: {mode}");
        _fs = new FileStream(resolvedPath,
                             mode,
                             FileAccess.Write,
                             FileShare.None,
                             4096,
                             FileOptions.Asynchronous);
    }

    protected override void ProcessRecord()
    {
        if (InputBytes.Length > 0)
        {
            _totalWriteBytes += InputBytes.Length;
            _writeCount++;
            PrintDebug($"Output chunk {InputBytes.Length} bytes");

            _fs?.Write(InputBytes, 0, InputBytes.Length);

            if (PassThru)
            {
                WriteObject(InputBytes, false);
            }
        }
    }

    protected override void StopProcessing()
    {
        WriteVerboseRaw($"Stopping process");
        _fs?.Dispose();
    }

    protected override void EndProcessing()
    {
        WriteVerboseRaw($"Output total: {_totalWriteBytes}, count: {_writeCount}");
        _fs?.Close();
        WriteVerboseRaw("Close");
    }
}
