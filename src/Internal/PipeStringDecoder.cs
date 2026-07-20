using System.Collections.Concurrent;
using System.IO.Pipes;
using System.Text;

namespace Sashimi.Internal;

internal sealed class PipeStringDecoder : IAsyncDisposable
{
    private readonly AnonymousPipeServerStream _server;
    private readonly AnonymousPipeClientStream _client;
    private readonly Task _pipeTask;
    private readonly BlockingCollection<RawOutputRecord> _output;
    private readonly RedirectTo _to;
    private readonly OutputFrom _from;

    public PipeStringDecoder(Encoding encoding,
                             BlockingCollection<RawOutputRecord> output,
                             RedirectTo to,
                             OutputFrom from)
    {
        _output = output;
        _to = to;
        _from = from;
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
            string? line;
            try
            {
                line = await sr.ReadLineAsync();
            }
            catch
            {
                break;
            }
            if (line is null)
                break;

            // PrintDebug("Set string line");
            _output.Add(new StringOutput(line, _to, _from));
        }

        try
        {
            var rest = sr.ReadToEnd();
            if (!string.IsNullOrEmpty(rest))
            {
                _output.Add(new StringOutput(rest, _to, _from));
            }
        }
        catch
        { }
    }

    public async ValueTask DisposeAsync()
    {
        await _server.DisposeAsync();
        await _pipeTask;
    }
}
