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
        Console.Error.WriteLine("[{0,-22}] {1,-20} {2}",
                                MyInvocation.MyCommand.Name,
                                $"{callerMethodName}:{callerLineNumber}:",
                                msg);
        Console.ResetColor();
    }

    protected void WriteVerboseRaw(ReadOnlySpan<char> message)
    {
        WriteVerbose($"[{MyInvocation.MyCommand.Name}] {message}");
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
