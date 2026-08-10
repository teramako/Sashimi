using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.IO.Pipes;
using System.Management.Automation;
using Sashimi.HexDump;
using Sashimi.Internal;

namespace Sashimi;

[Cmdlet(VerbsCommon.Show, "HexDump")]
[Alias("hexd")]
[OutputType(typeof(SplitView), typeof(UnifiedView))]
public class ShowHexDumpCommand : RawCommandBase
{
    [Parameter(ParameterSetName = "Data", Mandatory = true, ValueFromPipeline = true, Position = 0,
               HelpMessageBaseName = MessageBaseName, HelpMessageResourceId = "HexDump.parameters.Data")]
    [Alias("d")]
    public byte[] Data { get; set; } = [];

    [Parameter(ParameterSetName = "Path", Mandatory = true, Position = 0,
               HelpMessageBaseName = MessageBaseName, HelpMessageResourceId = "HexDump.parameters.Path")]
    public string Path { get; set; } = string.Empty;

    [Parameter(HelpMessageBaseName = MessageBaseName, HelpMessageResourceId = "HexDump.parameters.Config")]
    public Config? Config { get; set; }

    [Parameter(HelpMessageBaseName = MessageBaseName, HelpMessageResourceId = "HexDump.parameters.Encoding")]
    [Alias("e")]
    [ArgumentCompleter(typeof(EncodingCompleter))]
    public string Encoding { get; set; } = "utf-8";

    [Parameter(HelpMessageBaseName = MessageBaseName, HelpMessageResourceId = "HexDump.parameters.Offset")]
    [ValidateRange(0, long.MaxValue)]
    public long Offset { get; set; }

    [Parameter(HelpMessageBaseName = MessageBaseName, HelpMessageResourceId = "HexDump.parameters.Length")]
    [ValidateRange(0, int.MaxValue)]
    public int Length { get; set; }

    [Parameter(HelpMessageBaseName = MessageBaseName, HelpMessageResourceId = "HexDump.parameters.Color")]
    [Alias("c")]
    public ColorType Color { get; set; } = ColorType.None;

    [Parameter(HelpMessageBaseName = MessageBaseName, HelpMessageResourceId = "HexDump.parameters.View")]
    [Alias("v")]
    public ViewType View { get; set; }

    private Config _newConfig = Config.Default;

    private AnonymousPipeServerStream? _server;
    private Stream _client = null!;
    private Task _dumpTask = null!;

    private readonly BlockingCollection<CharCollectionRow> _output = new(256);

    private Func<CharCollectionRow, RowView> _createView = null!;

    [MemberNotNullWhen(true, nameof(_server))]
    private bool IsDataMode { get; set; }

    protected override void BeginProcessing()
    {
        _newConfig = (Config)(Config?.Clone() ?? Config.Default.Clone());

        try
        {
            var encoding = EncodingCompleter.GetEncoding(Encoding);
            _newConfig.Encoding = encoding;
            WriteVerboseRaw($"Set encoding: {encoding.WebName} [{encoding.EncodingName}]");
        }
        catch (ArgumentException ex)
        {
            ThrowTerminatingError(new(ex, "InvalidEncoding", ErrorCategory.InvalidArgument, Encoding));
            return;
        }

        if (Color is not ColorType.None)
        {
            _newConfig.ColorType = Color;
        }

        _createView = View switch
        {
            ViewType.Unified => row => new UnifiedView(row),
            _ => row => new SplitView(row)
        };

        if (string.IsNullOrEmpty(Path))
        {
            IsDataMode = true;
            _server = new AnonymousPipeServerStream(PipeDirection.Out, HandleInheritability.None);
            _client = new AnonymousPipeClientStream(PipeDirection.In, _server.ClientSafePipeHandle);
        }
        else
        {
            try
            {
                _client = File.OpenRead(Path);
            }
            catch (Exception ex)
            {
                ThrowTerminatingError(new(ex, "FailedToOpenFile", ErrorCategory.OpenError, Path));
                return;
            }

        }
        _dumpTask = Task.Run(async () => await ReadHexDumpedRowAsync(_client, PipelineStopToken));
    }

    private async Task ReadHexDumpedRowAsync(Stream stream, CancellationToken cancellationToken = default)
    {
        try
        {
            await foreach (var row in HexDumper.HexDumpAsync(stream, _newConfig, Offset, Length, cancellationToken))
            {
                DebugLog($"Add row: {row.Offset}");
                _output.Add(row, cancellationToken);
            }
        }
        catch (OperationCanceledException ex)
        {
            DebugLog(ex);
        }

        DebugLog("Completed: Reading rows");
        _output.CompleteAdding();
    }

    protected override void ProcessRecord()
    {
        if (IsDataMode && Data.Length > 0)
        {
            DebugLog($"Read {Data.Length} bytes from pipeline");
            _server.Write(Data, 0, Data.Length);
        }
    }

    protected override void StopProcessing()
    {
        try
        {
            _server?.Dispose();
            _client.Dispose();
        }
        catch
        { }
        finally
        {
            FlushDebugMessages();
        }
    }

    protected override void EndProcessing()
    {
        if (IsDataMode)
        {
            DebugLog($"Closing write stream: {_server}");
            _server.Dispose();
            FlushQueueAndWait();
        }
        else
        {
            FlushQueueAndWait();
        }

        FlushDebugMessages();
    }

    private void FlushQueueAndWait()
    {
        foreach (var row in _output.GetConsumingEnumerable(PipelineStopToken))
        {
            WriteObject(_createView(row));
        }
        DebugLog("Completed: Output rows"); 

        try
        {
            _dumpTask.Wait(PipelineStopToken);
            DebugLog("Completed: ReaderTask");
        }
        catch (AggregateException ex)
        {
            if (ex.InnerException is not null)
                throw ex.InnerException;
            throw;
        }
        finally
        {
            DebugLog($"Closing read stream: {_client}");
            _client.Dispose();
        }
    }
}
