using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Channels;

namespace Sashimi.HexDump;

public static partial class HexDumper
{
    [Conditional("DEBUG")]
    internal static void DebugLog(object msg,
                                  [CallerMemberName] string callerMethodName = "",
                                  [CallerLineNumber] int callerLineNumber = 0)
    {
#if DEBUG
        Sashimi.Internal.Logger.Instance?.Log(msg, "HexDump", callerMethodName, callerLineNumber);
#endif
    }

    /// <summary>
    /// Dump the content of <paramref name="stream"/>
    /// </summary>
    /// <param name="stream">The stream to be dumped</param>
    /// <param name="config">Various configuration objects for HexDump</param>
    /// <param name="offset">Position to start the dump</param>
    /// <param name="length">Length from the starting position of the dump</param>
    /// <exception cref="OperationCanceledException"></exception>
    public static async IAsyncEnumerable<CharCollectionRow> HexDumpAsync(Stream stream,
                                                                         Config config,
                                                                         long offset = 0,
                                                                         int length = 0,
                                                                         [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        long position = offset;
        var encoding = Encoding.GetEncoding(config.Encoding.CodePage, EncoderFallback.ReplacementFallback, new TopBytesFallback());
        var fallbackBuffer = ((TopBytesFallback)encoding.DecoderFallback).FallbackBuffer;
        CharCollectionRow charDatas = new(position, config);

        Channel<CharCollectionRow> rowChannel = Channel.CreateBounded<CharCollectionRow>(new BoundedChannelOptions(256)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = true,
            SingleWriter = true
        });

        var dumpTask = Task.Run(() =>
        {
            HexDumpStream(stream, config.Encoding, EmitBatch, offset, length, cancellationToken);
        });

        await foreach (var row in rowChannel.Reader.ReadAllAsync(cancellationToken))
        {
            yield return row;
        }

        await dumpTask;

        void EmitBatch(long p, ReadOnlySpan<CharData> batch)
        {
            if (p < 0)
            {
                if (!charDatas.IsEmpty)
                    rowChannel.Writer.TryWrite(charDatas);

                rowChannel.Writer.TryComplete();
                return;
            }
            for (var i = 0; i < batch.Length; i++)
            {
                var pos = p + i;
                charDatas.Set(pos, batch[i]);
                if ((pos & 0x0F) == 0x0F)
                {
                    rowChannel.Writer.TryWrite(charDatas);
                    charDatas = new(pos + 1, config);
                }
            }
        }
    }

    private const int BUFFER_LENGTH = 1024;

    private static void Seek(Stream stream, long offset)
    {
        if (offset > 0)
        {
            if (stream.CanSeek)
            {
                DebugLog($"Seek to {offset} using the stream.Seek()");
                stream.Seek(offset, SeekOrigin.Begin);
            }
            else
            {
                DebugLog($"Seek to {offset} manualy");
                long remaining = offset;
                Span<byte> skip = stackalloc byte[BUFFER_LENGTH];
                while (remaining > 0)
                {
                    int toRead = (int)Math.Min(skip.Length, remaining);
                    int read = stream.Read(skip[..toRead]);
                    if (read <= 0)
                        return;

                    remaining -= read;
                }
            }
        }
    }

    internal static void HexDumpStream(Stream stream,
                                       Encoding originalEncoding,
                                       Action<long, ReadOnlySpan<CharData>> emitBatch,
                                       long offset = 0,
                                       int length = 0,
                                       CancellationToken cancellationToken = default)
    {
        if (offset > 0)
            Seek(stream, offset);

        long position = offset;
        int remaining = length > 0 ? length : int.MaxValue;

        try
        {
            switch (originalEncoding.CodePage)
            {
                case 20127: // ASCII
                    DebugLog("use special encoding processor for ASCII");
                    ProcessingFixedByteEncoding(stream,
                                                position,
                                                remaining,
                                                emitBatch,
                                                HexDumpCoreAscii,
                                                cancellationToken);
                    return;
                case 28591: // Latin-1
                    DebugLog("use special encoding processor for Latin-1");
                    ProcessingFixedByteEncoding(stream,
                                                position,
                                                remaining,
                                                emitBatch,
                                                HexDumpCoreLatin1,
                                                cancellationToken);
                    return;
                case 65001: // UTF-8
                    DebugLog("use special encoding processor for UTF-8");
                    ProcessingUtf8(stream,
                                   position,
                                   remaining,
                                   emitBatch,
                                   cancellationToken);
                    return;
                default:
                    DebugLog($"use generic encoding processor for {originalEncoding.WebName}");
                    ProcessGeneric(stream,
                                   position,
                                   remaining,
                                   emitBatch,
                                   originalEncoding,
                                   cancellationToken);
                    return;
            }
        }
        finally
        {
            emitBatch(-1, default);
        }
    }

    private static void ProcessingFixedByteEncoding(Stream stream,
                                                    long position,
                                                    int remaining,
                                                    Action<long, ReadOnlySpan<CharData>> emitBatch,
                                                    Action<ReadOnlySpan<byte>, long, Action<long, ReadOnlySpan<CharData>>> core,
                                                    CancellationToken cancellationToken = default)
    {
        Span<byte> buffer = stackalloc byte[BUFFER_LENGTH];
        int readBytes;
        do
        {
            if (cancellationToken.IsCancellationRequested)
                return;

            var buf = buffer[..Math.Min(BUFFER_LENGTH, remaining)];
            readBytes = stream.Read(buf);
            if (readBytes == 0)
                break;
            core(buf[..readBytes], position, emitBatch);
            remaining -= readBytes;
            position += readBytes;
        }
        while (remaining > 0);
    }

    private static void ProcessingUtf8(Stream stream,
                                       long position,
                                       int remaining,
                                       Action<long, ReadOnlySpan<CharData>> emitBatch,
                                       CancellationToken cancellationToken = default)
    {
        var fallbackBuffer = new TopBytesFallback.TopByteFallbackBuffer();
        ReadOnlySpan<byte> fbBytes = ReadOnlySpan<byte>.Empty;

        Span<byte> buffer = stackalloc byte[BUFFER_LENGTH];
        int readBytes, totalBytes;
        do
        {
            if (cancellationToken.IsCancellationRequested)
                return;

            var buf = buffer[..Math.Min(BUFFER_LENGTH, remaining + fbBytes.Length)];
            readBytes = stream.Read(buf[fbBytes.Length..]);
            if (readBytes == 0)
                break;

            fbBytes.CopyTo(buf);
            totalBytes = readBytes + fbBytes.Length;
            HexDumpCoreUTF8(buf[..totalBytes], position - fbBytes.Length, emitBatch, fallbackBuffer);
            fbBytes = fallbackBuffer.DrainFallbackBytes();

            remaining -= readBytes;
            position += readBytes;
        }
        while (remaining > 0);

        FlashFallbackBytes(fbBytes, position, emitBatch);
    }

    private static void ProcessGeneric(Stream stream,
                                       long position,
                                       int remaining,
                                       Action<long, ReadOnlySpan<CharData>> emitBatch,
                                       Encoding originalEncoding,
                                       CancellationToken cancellationToken = default)
    {
        var encoding = Encoding.GetEncoding(originalEncoding.CodePage, EncoderFallback.ReplacementFallback, new TopBytesFallback());
        var fallbackBuffer = ((TopBytesFallback)encoding.DecoderFallback).FallbackBuffer;
        ReadOnlySpan<byte> fbBytes = ReadOnlySpan<byte>.Empty;

        Span<byte> buffer = stackalloc byte[BUFFER_LENGTH];
        int readBytes, totalBytes;
        do
        {
            if (cancellationToken.IsCancellationRequested)
                return;

            var buf = buffer[..Math.Min(BUFFER_LENGTH, remaining + fbBytes.Length)];
            readBytes = stream.Read(buf[fbBytes.Length..]);
            if (readBytes == 0)
                break;

            fbBytes.CopyTo(buf);
            totalBytes = readBytes + fbBytes.Length;
            HexDumpCore(buf[..totalBytes], encoding, fallbackBuffer, position - fbBytes.Length, emitBatch);
            fbBytes = fallbackBuffer.DrainFallbackBytes();

            remaining -= readBytes;
            position += readBytes;
        }
        while (remaining > 0);

        FlashFallbackBytes(fbBytes, position, emitBatch);
    }

    private static void FlashFallbackBytes(ReadOnlySpan<byte> fbBytes, long position, Action<long, ReadOnlySpan<CharData>> emitBatch)
    {
        if (fbBytes.IsEmpty)
            return;

        Span<CharData> batch = stackalloc CharData[fbBytes.Length];
        position -= fbBytes.Length;

        for (int i = 0; i < fbBytes.Length; i++)
        {
            batch[i] = new(fbBytes[i], (char)fbBytes[i], CharType.Binary);
            DebugLog($"p={position+i:X8}: (Fallback) {fbBytes[i]:X2}");
        }
        emitBatch(position, batch);
    }

    private static void HexDumpCore(ReadOnlySpan<byte> data,
                                    Encoding encoding,
                                    TopBytesFallback.TopByteFallbackBuffer fallbackBuffer,
                                    long startPosition,
                                    Action<long, ReadOnlySpan<CharData>> emitBatch)
    {
        const int BYTE_LENGTH = 4;
        int pos = 0;
        Span<char> charBuf = stackalloc char[8];
        Span<byte> byteBuf = stackalloc byte[BYTE_LENGTH];
        scoped Span<byte> remainingBytes = Span<byte>.Empty;
        Span<CharData> batch = stackalloc CharData[4];
        long globalPosition;

        DebugLog($"HexDumpCore: Start {startPosition} Lengt={data.Length}");
        while (pos < data.Length)
        {
            int byteBufLength = Math.Min(BYTE_LENGTH, data.Length - pos);
            if (remainingBytes.IsEmpty)
            {
                data[pos..(pos + byteBufLength)].CopyTo(byteBuf);
            }
            else
            {
                remainingBytes.CopyTo(byteBuf);
                DebugLog($"pos = {pos}, remainingBytes = {remainingBytes.Length}, byteBufLength = {byteBufLength}");
                if (pos + byteBufLength <= data.Length)
                {
                    data[(pos + remainingBytes.Length)..(pos + byteBufLength)].CopyTo(byteBuf[remainingBytes.Length..]);
                }
            }
            if (remainingBytes.Length + byteBufLength < BUFFER_LENGTH)
            {
                byteBuf = byteBuf[..byteBufLength];
            }
            DebugLog($"TryGetChars([{string.Join(", ", byteBuf.ToArray().Select(b => $"{b:X2}"))}])");
            if (!encoding.TryGetChars(byteBuf, charBuf, out int charsWritten))
            {
                throw new DecoderFallbackException($"", byteBuf.ToArray(), 0);
            }

            globalPosition = startPosition + pos;
            var byteIndex = 0;

            if (fallbackBuffer.HasFallbackChars)
            {
                DebugLog($"HasFallbachChars: pos={pos}, remaiing={fallbackBuffer.Remaining}, dataLength={data.Length}");
                if (pos + fallbackBuffer.Remaining >= data.Length)
                {
                    DebugLog("Return");
                    return;
                }

                var fbBytes = fallbackBuffer.DrainFallbackBytes();
                for (; byteIndex < fbBytes.Length; byteIndex++)
                {
                    byte fbByte = fbBytes[byteIndex];
                    DebugLog($"p={pos+byteIndex:X8}: (Fallback) {fbByte:X2}");
                    batch[byteIndex] = new(fbByte, (char)fbByte, CharType.Binary);
                }
                emitBatch(globalPosition, batch[..byteIndex]);
                pos += byteIndex;
                remainingBytes = byteBuf[byteIndex..];
                DebugLog($"END: pos={pos}, remainingBytes={string.Join(' ', remainingBytes.ToArray().Select(b => $"{b:X2}"))}");
                continue;
            }

            Rune.DecodeFromUtf16(charBuf, out Rune rune, out _);
            int byteCount = encoding.GetByteCount(rune.ToString());
            CharType type = byteCount > 1 ? CharType.MultiByteChar : CharType.SingleByteChar;
            CharData charData = new(byteBuf[byteIndex], rune, type);
            DebugLog($"p={pos:X8}: [{byteCount}] {charData}");

            batch[0] = charData;

            for (var j = 1; j < byteCount; j++)
            {
                type = CharType.ContinuationByte;
                if (j == 1) type |= CharType.First;
                if (j == byteCount - 1) type |= CharType.Last;
                batch[j] = new(byteBuf[byteIndex + j], rune, type);
            }
            emitBatch(globalPosition, batch[..byteCount]);

            pos += byteCount;
            remainingBytes = byteBuf[byteCount..];
            DebugLog($"LOOP END: pos={pos}, remainingBytes={string.Join(' ', remainingBytes.ToArray().Select(b => $"{b:X2}"))}");
        }
    }
}
