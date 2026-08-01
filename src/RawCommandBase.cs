using System.Diagnostics;
using System.Management.Automation;
using System.Runtime.CompilerServices;
using Sashimi.Internal;

namespace Sashimi;

public abstract class RawCommandBase : PSCmdlet
{
#if DEBUG
    internal Logger DebugLogger => field ?? Logger.GetLogger(MyInvocation.HistoryId);
#endif

    [Conditional("DEBUG")]
    public void DebugLog(object msg, [CallerMemberName] string callerMethodName = "", [CallerLineNumber] int callerLineNumber = 0)
    {
#if DEBUG
        DebugLogger.Log(msg, MyInvocation.MyCommand.Name, callerMethodName, callerLineNumber);
#endif
    }

    [Conditional("DEBUG")]
    public void FlushDebugMessages()
    {
#if DEBUG
        DebugLogger.Print();
#endif
    }

    protected const string MessageBaseName = "Sashimi.resources.messages";

    /// <inheritdoc cref="CommandInfo.Name"/>
    public string MyCommandName => MyInvocation.MyCommand.Name;

    private readonly Stopwatch _sw = Stopwatch.StartNew();

    internal void WriteVerboseRaw(ReadOnlySpan<char> message)
    {
        DebugLog($"Verbose: {message}");
        WriteVerbose($"({_sw.Elapsed})[{MyCommandName}] {message}");
    }

    protected void WriteInformationRaw(string message, params string[] tags)
        => WriteInformationRaw(message, null, null, tags);

    protected void WriteInformationRaw(string message, ConsoleColor? fg, ConsoleColor? bg, params string[] tags)
    {
        var messageData = new HostInformationMessage()
        {
            ForegroundColor = fg,
            BackgroundColor = bg,
            Message = message,
            NoNewLine = false
        };
        WriteInformation(messageData, tags);
    }

    protected ApplicationInfo GetAppInfo(string name)
            => InvokeCommand.GetCommand(name, CommandTypes.Application) as ApplicationInfo
               ?? throw new CommandNotFoundException($"raw: command '{name}' not found");

    /// <summary>
    /// Set `LASTEXITCODE` to <paramref name="exitCode"/>
    /// </summary>
    public void SetLastExitCode(int exitCode)
    {
        SessionState.PSVariable.Set("LASTEXITCODE", exitCode);
    }
}
