#if DEBUG
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text;

namespace Sashimi.Internal;

internal class Logger
{
    private static long _currentId = -1;
    private static readonly object _sync = new();

    internal static Logger? Instance { get; private set; } = null;

    /// <summary>
    /// Gets the <see cref="Logger"/> instance associated with the current invocation.
    /// <para>
    /// This logger is a DEBUG-only, per-pipeline singleton. The same instance is reused
    /// for all components (cmdlet, process runner, decoders) participating in a single
    /// PowerShell pipeline execution.
    /// </para>
    /// <para>
    /// When the <paramref name="id"/> (HistoryId) changes, the logger is reset:
    /// its internal message queue is cleared and its stopwatch is restarted. This
    /// reflects the fact that PowerShell executes only one pipeline at a time, and
    /// each pipeline has a unique HistoryId.
    /// </para>
    /// <para>
    /// Thread-safety:
    /// Access to the singleton instance is protected by a lock. This ensures that
    /// resetting the logger when a new HistoryId is encountered is atomic, even if
    /// multiple DEBUG log calls occur concurrently.
    /// </para>
    /// </summary>
    /// <remarks>
    /// Note:
    /// This design intentionally discards logs from previous invocations when a new
    /// pipeline starts. If concurrent pipelines were ever supported, a different
    /// mapping strategy (e.g., per-id loggers) would be required.
    /// </remarks>
    /// <param name="id">The HistoryId of the current PowerShell invocation.</param>
    /// <returns>The logger instance for the current invocation.</returns>
    public static Logger GetLogger(long id)
    {
        lock (_sync)
        {
            Instance ??= new();
            if (id != _currentId)
            {
                _currentId = id;
                Instance._messages.Clear();
                Instance._sw.Restart();
            }
            return Instance;
        }
    }

    private Logger()
    {
    }

    record Msg(TimeSpan TimeSpan, string Category, string Source, object Message)
    {
        public override string ToString()
            => $"({TimeSpan}){Source,-25} {Category,22}: {Message}";
    }
    record ProcMsg(TimeSpan TimeSpan, int Pid, string Category, string Source, object Message)
        : Msg(TimeSpan, Category, Source, Message)
    {
        public override string ToString()
            => $"({TimeSpan})[{Pid}]{Source,-25} {Category,14}: {Message}";
    }

    private readonly Stopwatch _sw = Stopwatch.StartNew();
    private readonly ConcurrentQueue<Msg> _messages = new();

    public void Log(object msg, int pid, string category, [CallerMemberName] string callerMethodName = "", [CallerLineNumber] int callerLineNumber = 0)
    {
        _messages.Enqueue(new ProcMsg(_sw.Elapsed, pid, category, $"{callerMethodName}:{callerLineNumber}", msg));
    }
    public void Log(object msg, string category, [CallerMemberName] string callerMethodName = "", [CallerLineNumber] int callerLineNumber = 0)
    {
        _messages.Enqueue(new Msg(_sw.Elapsed, category, $"{callerMethodName}:{callerLineNumber}", msg));
    }

    public void Print(string fg = "\e[90m")
    {
        StringBuilder sb = new(fg);
        while (_messages.TryDequeue(out var msg))
        {
            sb.AppendLine(msg.ToString());
        }
        sb.Append("\e[0m");
        Console.Error.Write(sb);
    }
}
#endif
