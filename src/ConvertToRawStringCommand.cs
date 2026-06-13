using System.Collections.Concurrent;
using System.IO.Pipes;
using System.Management.Automation;
using System.Text;

namespace Sashimi;

[Cmdlet(VerbsData.ConvertTo, "RawString")]
[OutputType(typeof(string))]
[Alias("b2a")]
public sealed class ConvertToRawStringCommand : PSCmdlet
{
    [Parameter(Mandatory = true, ValueFromPipeline = true)]
    public byte[] InputBytes { get; set; } = null!;

    [Parameter()]
    [Alias("e")]
    public string Encoding { get; set; } = "utf-8";

    private AnonymousPipeServerStream? _server;
    private AnonymousPipeClientStream? _client;
    private Task? _readerTask;

    private readonly ConcurrentQueue<string> _queue = [];
    private readonly AutoResetEvent _queueEvent = new(false);
    private volatile bool _readerCompleted = false;

    private long _totalReadBytes;
    private int _readCount;

    protected override void BeginProcessing()
    {
        _server = new(PipeDirection.Out, HandleInheritability.None);
        _client = new(PipeDirection.In, _server.ClientSafePipeHandle);
        var encoding = System.Text.Encoding.GetEncoding(Encoding);
        WriteVerbose($"[{MyInvocation.MyCommand.Name}] Set encoding: {encoding.WebName} [{encoding.EncodingName}]");
        _readerTask = Task.Run(() => Decode(_client, encoding));
    }

    private void Decode(Stream stream, Encoding encoding)
    {
        using var sr = new StreamReader(stream, encoding);
        string? line;
        while ((line = sr.ReadLine()) is not null)
        {
            _queue.Enqueue(line);
            _queueEvent.Set();
        }

        var rest = sr.ReadToEnd();
        if (!string.IsNullOrEmpty(rest))
        {
            _queue.Enqueue(rest);
        }
        _readerCompleted = true;
        _queueEvent.Set();
    }

    protected override void StopProcessing()
    {
        _server?.Dispose();
        _client?.Dispose();
    }

    protected override void ProcessRecord()
    {
        if (InputBytes.Length > 0)
        {
            _totalReadBytes += InputBytes.Length;
            _readCount++;
            WriteInformation($"Read {InputBytes.Length} bytes from pipeline", ["Sashimi.Raw.ReadChunk"]);
            _server?.Write(InputBytes, 0, InputBytes.Length);
        }
    }

    protected override void EndProcessing()
    {
        if (_totalReadBytes > 0)
        {
            WriteVerbose($"[{MyInvocation.MyCommand.Name}] Read total: {_totalReadBytes}, count: {_readCount}");
        }
        _server?.Close();

        int lineCount = 0;
        while (!_readerCompleted || !_queue.IsEmpty)
        {
            while (_queue.TryDequeue(out var line))
            {
                lineCount++;
                WriteInformation($"Output line: [{lineCount}] {line}", ["Sashimi.Raw.OutputLine"]);
                WriteObject(line);
            }

            if (!_readerCompleted)
            {
                WriteInformation("Wait", ["Sashimi.Raw.Wait"]);
                _queueEvent.WaitOne();
            }
        }

        if (lineCount > 0)
        {
            WriteVerbose($"[{MyInvocation.MyCommand.Name}] Output total line: {lineCount}");
        }

        try
        {
            _readerTask?.Wait();
        }
        catch
        {
        }
    }
}
