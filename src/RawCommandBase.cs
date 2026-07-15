using System.Diagnostics;
using System.Management.Automation;
using System.Runtime.CompilerServices;

namespace Sashimi;

public abstract class RawCommandBase : PSCmdlet
{
    [Conditional("DEBUG")]
    protected void PrintDebug(string msg,
                              ConsoleColor fg = ConsoleColor.DarkGray,
                              [CallerMemberName] string callerMethodName = "",
                              [CallerLineNumber] int callerLineNumber = 0)
    {
        Console.ForegroundColor = fg;
        Console.Error.WriteLine("({0})[{1,-22}] {2,-20} {3}",
                                _sw.Elapsed,
                                MyInvocation.MyCommand.Name,
                                $"{callerMethodName}:{callerLineNumber}:",
                                msg);
        Console.ResetColor();
    }

    protected const string MessageBaseName = "Sashimi.resources.messages";

    /// <inheritdoc cref="CommandInfo.Name"/>
    public string MyCommandName => MyInvocation.MyCommand.Name;

    private readonly Stopwatch _sw = Stopwatch.StartNew();

    internal void WriteVerboseRaw(ReadOnlySpan<char> message)
    {
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
}
