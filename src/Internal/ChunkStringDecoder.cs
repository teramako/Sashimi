using System.Text;

namespace Sashimi.Internal;

/// <summary>
/// Decode bytes to string synchonously.
/// </summary>
/// <remarks>
/// ChunkStringDecoder is not thread-safe.
/// Each instance is used only from a single producer thread (stdout or stderr).
/// </remarks>
/// <param name="encoding">The encoding used for decoding</param>
/// <param name="emit">Callback action that processes the decoded string</param>
/// <param name="rawMode">
/// <see langword="false"/> (default) indicates emit the decoded string per line;
/// <see langword="true"/> emit the entire string at the end.
/// </param>
internal sealed class ChunkStringDecoder(Encoding encoding, Action<ReadOnlySpan<char>> emit, bool rawMode = false)
{
    public Encoding Encoding { get; } = encoding;
    public bool RawMode { get; } = rawMode;

    private Decoder _decoder = encoding.GetDecoder();
    private StringBuilder _stringBuffer = new();
    private const int bufferSize = 1024;
    private readonly Action<ReadOnlySpan<char>> _emit = emit;
    private bool _pendingCR;

    public void Decode(ReadOnlySpan<byte> bytes)
    {
        var remaining = bytes;
        Span<char> charBuffer = stackalloc char[bufferSize];
        while (!remaining.IsEmpty)
        {
            _decoder.Convert(remaining, charBuffer, false, out var bytesUsed, out var charsUsed, out var completed);

            if (charsUsed == 0)
                return;

            ReadOnlySpan<char> chars = charBuffer[..charsUsed];
            if (RawMode)
            {
                _stringBuffer.Append(chars);
            }
            else
            {
                EmitLines(chars);
            }

            remaining = remaining[bytesUsed..];
        }
    }

    private void EmitLines(ReadOnlySpan<char> chars)
    {
        ReadOnlySpan<char> stringChunk;
        if (_stringBuffer.Length == 0)
        {
            stringChunk = chars;
        }
        else if (_pendingCR && _stringBuffer.Length == 1 && _stringBuffer[0] is '\r')
        {
            _stringBuffer.Clear();
            if (chars[0] is '\n')
            {
                stringChunk = chars[1..];
            }
            else
            {
                stringChunk = chars;
            }
        }
        else
        {
            _stringBuffer.Append(chars);
            stringChunk = _stringBuffer.ToString();
            _stringBuffer.Clear();
        }
        int i;
        while ((i = stringChunk.IndexOfAny("\r\n")) >= 0)
        {
            var line = stringChunk[..i];
            _emit(line);

            char d = stringChunk[i];
            if (d is '\r')
            {
                if (i + 1 == stringChunk.Length)
                {
                    _stringBuffer.Append(d);
                    _pendingCR = true;
                    return;
                }
                else if (stringChunk[i + 1] is '\n')
                {
                    i += 1;
                }
            }
            stringChunk = stringChunk[(i + 1)..];
        }

        if (!stringChunk.IsEmpty)
            _stringBuffer.Append(stringChunk);
    }

    public void EmitRemaining()
    {
        Span<char> finalBuffer = stackalloc char[bufferSize];
        _decoder.Convert(Array.Empty<byte>(), finalBuffer, true, out var bytesUsed, out var charsUsed, out var completed);
        if (charsUsed > 0)
        {
            _stringBuffer.Append(finalBuffer[..charsUsed]);
        }

        if (_stringBuffer.Length > 0)
        {
            _emit(_stringBuffer.ToString());
        }
    }
}
