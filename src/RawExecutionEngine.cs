using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO.Pipes;
using System.Management.Automation;
using System.Runtime.CompilerServices;
using System.Text;

namespace Sashimi;

internal class RawExecutionEngine
{
    private AnonymousPipeServerStream? _stringServer;
    private AnonymousPipeClientStream? _stringClient;
    private Task? _stringReaderTask;
    private Decoder _stderrDecoder;
    private long _totalReadBytes;
    private int _readCount;

    public string CommandPath { get; }
    public string[] Arguments { get; }
    public Encoding Encoding { get; }
    [MemberNotNullWhen(true, nameof(_stringServer), nameof(_stringClient), nameof(_stringReaderTask))]
    public bool AsString { get; }
    public OutputType OutputType { get; }

    public DateTime StartTime => Runner.StartTime;
    public DateTime ExitTime => Runner.ExitTime;
    public string LogPrefix => $"[{Runner.Pid}][{CommandPath}]";

    public RawProcessRunner Runner { get; }
    public BlockingCollection<RawOutputItem> Output { get; } = new(1024);

    public RawExecutionEngine(string commandPath,
                              string[] arguments,
                              Encoding encoding,
                              bool asString,
                              OutputType outputType)
    {
        CommandPath = commandPath;
        Arguments = arguments;
        Encoding = encoding;
        AsString = asString;
        OutputType = outputType;
        _stderrDecoder = encoding.GetDecoder();
        Runner = new RawProcessRunner(CommandPath, Arguments);
    }

    public void StartAsync(CancellationToken cancellationToken)
    {
        if (AsString)
        {
            _stringServer = new(PipeDirection.Out, HandleInheritability.None);
            _stringClient = new(PipeDirection.In, _stringServer.ClientSafePipeHandle);
            if (OutputType.HasFlag(OutputType.Stdout))
            {
                Runner.OnStdout += OnOutputChunkAsString;
            }
            if (OutputType.HasFlag(OutputType.Stderr))
            {
                Runner.OnStderr += OnOutputChunkAsString;
            }
            else
            {
                Runner.OnStderr += OnErrorChunk;
            }
            _stringReaderTask = AsyncDecode(_stringClient, Encoding);
        }
        else
        {
            if (OutputType.HasFlag(OutputType.Stdout))
            {
                Runner.OnStdout += OnOutputChunk;
            }
            if (OutputType.HasFlag(OutputType.Stderr))
            {
                Runner.OnStderr += OnOutputChunk;
            }
            else
            {
                Runner.OnStderr += OnErrorChunk;
            }
        }
        Runner.Start(cancellationToken);
    }

    private void OnOutputChunk(byte[] chunk)
    {
        Output.Add(new ChunkOutput(chunk));
    }

    private void OnOutputChunkAsString(byte[] chunk)
    {
        if (chunk.Length > 0 && _stringServer is not null)
        {
            _stringServer.Write(chunk, 0, chunk.Length);
            PrintDebug($"Write {chunk.Length} bytes to stringServer");
            _stringServer.Flush();
        }
    }

    private void OnErrorChunk(byte[] chunk)
    {
        if (_stderrDecoder is null)
            return;

        PrintDebug($"Write StdErr: {chunk.Length} bytes");
        var charCount = _stderrDecoder.GetCharCount(chunk, false);
        Span<char> text = stackalloc char[charCount];
        _stderrDecoder.Convert(chunk, text, false, out _, out var charsUsed, out _);
        var record = new InformationRecord($"{PSStyle.Instance.Formatting.Error}{text[..charsUsed].TrimEnd("\r\n")}{PSStyle.Instance.Reset}",
                                           $"{Runner.Name} (PID: {Runner.Pid})");
        record.Tags.AddRange("PSHOST", "stderr");
        Output.Add(new InformationOutput(record));

        // NOTE:
        // We intentionally do NOT flush the stderr decoder at stream end.
        // Any remaining bytes in the decoder represent incomplete or undecodable sequences.
        // Flushing would emit fallback characters, which is undesirable:
        // stderr should show only successfully decoded text, and undecodable bytes are ignored rather than replaced.
    }

    private async Task AsyncDecode(Stream stream, Encoding encoding)
    {
        using var sr = new StreamReader(stream, encoding);
        while (true)
        {
            string? line = await sr.ReadLineAsync();
            if (line is null)
                break;

            PrintDebug("Set string line");
            Output.Add(new StringOutput(line));
        }

        var rest = await sr.ReadToEndAsync();
        if (!string.IsNullOrEmpty(rest))
        {
            Output.Add(new StringOutput(rest));
        }
        PrintDebug("Complete stringReader");
        Output.CompleteAdding();
    }

    public async Task WriteInputAsync(byte[] inputBytes, CancellationToken cancellationToken)
    {
        _totalReadBytes += inputBytes.Length;
        _readCount++;
        PrintDebug($"Read {inputBytes.Length} bytes from pipeline");
        await Runner.WriteStdinAsync(inputBytes, cancellationToken);
    }

    public void Kill()
    {
        Runner.Kill();
        _stringServer?.Dispose();
        _stringClient?.Dispose();
    }

    public async Task<int> WaitForExitAsync(CancellationToken cancellationToken)
    {
        Runner.CloseStdin();

        int exitCode;
        PrintDebug($"Wait process runner's output to finish");
        exitCode = await Runner.WaitForCompleteAsync(cancellationToken);

        if (AsString)
        {
            _stringServer.Close();
            await _stringReaderTask;
        }
        else
        {
            PrintDebug("Complete queueInput");
            Output.CompleteAdding();
        }
        return exitCode;
    }

    [Conditional("DEBUG")]
    public void PrintDebug(string msg,
                           [CallerMemberName] string callerMethodName = "",
                           [CallerLineNumber] int callerLineNumber = 0)
    {
        Runner.Log($"{msg}", "cmdlet", callerMethodName, callerLineNumber);
    }

    [Conditional("DEBUG")]
    public void PrintDebugMessages()
    {
        foreach (var msg in Runner.DebugMsgs)
        {
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.Error.WriteLine(msg);
        }
        Console.ResetColor();
    }
}
