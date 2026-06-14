using System.Management.Automation;
using System.Text;

namespace Sashimi;

[Cmdlet(VerbsData.ConvertTo, "RawString")]
[OutputType(typeof(string))]
[Alias("b2a")]
public sealed class ConvertToRawStringCommand : RawCommandBase
{
    [Parameter(Mandatory = true, ValueFromPipeline = true, Position = 0)]
    public byte[] InputBytes { get; set; } = null!;

    [Parameter()]
    [Alias("e")]
    public string Encoding { get; set; } = "utf-8";

    [Parameter()]
    [Alias("r")]
    public SwitchParameter Raw { get; set; }

    private long _totalReadBytes;
    private int _readCount;
    private int _lineCount;

    private const int bufferSize = 4096;
    private StringBuilder _stringBuffer = new(bufferSize);
    private Decoder _decoder = null!;
    private bool _pendingCR;

    protected override void BeginProcessing()
    {
        var encoding = System.Text.Encoding.GetEncoding(Encoding);
        _decoder = encoding.GetDecoder();
        WriteVerboseRaw($"Set encoding: {encoding.WebName} [{encoding.EncodingName}]");
    }

    protected override void ProcessRecord()
    {
        if (InputBytes.Length > 0)
        {
            _totalReadBytes += InputBytes.Length;
            _readCount++;

            Span<char> charBuffer = stackalloc char[bufferSize];
            _decoder.Convert(InputBytes, charBuffer, false, out var bytesUsed, out var charsUsed, out var completed);

            if (charsUsed == 0)
                return;

            PrintDebug($"readChars: {charsUsed}");

            ReadOnlySpan<char> chars = charBuffer[..charsUsed];
            if (Raw)
            {
                _stringBuffer.Append(chars);
            }
            else
            {
                scoped ReadOnlySpan<char> stringChunk;
                if (_pendingCR && _stringBuffer.Length == 1 && _stringBuffer[0] is '\r')
                {
                    _stringBuffer.Clear();
                    if (chars[0] is '\n')
                    {
                        PrintDebug("found CRLF: 0-1");
                        stringChunk = chars[1..];
                    }
                    else
                    {
                        PrintDebug("found CR: 0");
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
                    WriteObject(line.ToString());
                    _lineCount++;
                    PrintDebug($"[{_lineCount}] Output {line.Length} chars");

                    char d = stringChunk[i];
                    if (d is '\r')
                    {
                        if (i + 1 == stringChunk.Length)
                        {
                            _stringBuffer.Append(d);
                            _pendingCR = true;
                            PrintDebug("Set pendingCR");
                            return;
                        }
                        else if (stringChunk[i + 1] is '\n')
                        {
                            PrintDebug($"found CRLF: {i}-{i + 1}");
                            i += 1;
                        }
                    }
                    else
                    {
                        PrintDebug($"found LF: {i}");
                    }
                    stringChunk = stringChunk[(i + 1)..];
                }

                if (!stringChunk.IsEmpty)
                    _stringBuffer.Append(stringChunk);
            }
        }
    }

    protected override void EndProcessing()
    {
        Span<char> finalBuffer = stackalloc char[4096];
        _decoder.Convert(Array.Empty<byte>(), finalBuffer, true, out var bytesUsed, out var charsUsed, out var completed);
        if (charsUsed > 0)
        {
            _stringBuffer.Append(finalBuffer[..charsUsed]);
        }

        if (_totalReadBytes > 0)
        {
            WriteVerboseRaw($"Read total: {_totalReadBytes}, count: {_readCount}");
        }

        if (Raw)
        {
            WriteObject(_stringBuffer.ToString());
            return;
        }

        if (_stringBuffer.Length > 0)
        {
            var line = _stringBuffer.ToString();
            WriteObject(line);
            _lineCount++;
            PrintDebug($"[{_lineCount}] Output {line.Length} chars (final)");
        }

        if (_lineCount > 0)
        {
            WriteVerboseRaw($"Output total line: {_lineCount}");
        }
    }
}
