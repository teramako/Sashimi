using System.Collections.Concurrent;
using System.IO.Pipes;
using System.Management.Automation;
using System.Text;

namespace Sashimi;

[Cmdlet(VerbsData.ConvertTo, "String")]
[OutputType(typeof(string))]
[Alias("b2a")]
public sealed class ConvertToStringCommand : Cmdlet
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

    protected override void BeginProcessing()
    {
        _server = new(PipeDirection.Out, HandleInheritability.None);
        _client = new(PipeDirection.In, _server.ClientSafePipeHandle);
        var encoding = System.Text.Encoding.GetEncoding(Encoding);
        WriteVerbose($"[ConvertToString] Set encoding: {encoding.WebName} [{encoding.EncodingName}]");
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
            WriteVerbose($"[ConvertToString] Read {InputBytes.Length} bytes from pipeline");
            _server?.Write(InputBytes, 0, InputBytes.Length);
        }
    }

    protected override void EndProcessing()
    {
        _server?.Close();

        while (!_readerCompleted || !_queue.IsEmpty)
        {
            while (_queue.TryDequeue(out var line))
            {
                WriteVerbose($"[ConvertToString] Output [{line}]");
                WriteObject(line);
            }

            if (!_readerCompleted)
            {
                WriteVerbose($"[ConvertToString] Wait");
                _queueEvent.WaitOne();
            }
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
