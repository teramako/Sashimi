using System.Collections.Concurrent;
using System.IO.Pipes;
using System.Text;

namespace Sashimi.Internal;

internal sealed class PipeStringDecoder : IAsyncDisposable
{
    private readonly AnonymousPipeServerStream _server;
    private readonly AnonymousPipeClientStream _client;
    private readonly Task _pipeTask;
    private readonly BlockingCollection<RawOutputItem> _output;
    private readonly OutputType _to;

    public PipeStringDecoder(Encoding encoding, BlockingCollection<RawOutputItem> output, OutputType to)
    {
        _output = output;
        _to = to;
        _server = new(PipeDirection.Out, HandleInheritability.None);
        _client = new(PipeDirection.In, _server.ClientSafePipeHandle);
        _pipeTask = AsyncDecode(_client, encoding);
    }

    public void WriteBytes(ReadOnlySpan<byte> inputBytes)
    {
        _server.Write(inputBytes);
    }

    public void Close()
    {
        _server.Dispose();
    }

    private async Task AsyncDecode(Stream stream, Encoding encoding)
    {
        using var sr = new StreamReader(stream, encoding);
        while (true)
        {
            string? line = await sr.ReadLineAsync();
            if (line is null)
                break;

            // PrintDebug("Set string line");
            _output.Add(new StringOutput(line, _to));
        }

        var rest = await sr.ReadToEndAsync();
        if (!string.IsNullOrEmpty(rest))
        {
            _output.Add(new StringOutput(rest, _to));
        }
    }

    public async ValueTask DisposeAsync()
    {
        await _server.DisposeAsync();
        await _pipeTask;
    }
}
