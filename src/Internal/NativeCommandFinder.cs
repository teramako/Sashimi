using System.Management.Automation;
using System.Management.Automation.Language;

namespace Sashimi.Internal;

/// <summary>
/// A <see cref="AstVisitor"/> that scans the ScriptBlock's AST and collects
/// command names that can be resolved as external commands (<see cref="ApplicationInfo"/>).
/// </summary>
internal sealed class NativeCommandFinder(PSCmdlet cmdlet) : AstVisitor
{
    public readonly record struct NativeCommandItem(CommandAst Ast, ApplicationInfo AppInfo);

    public List<NativeCommandItem> NativeCommands { get; } = [];

    public override AstVisitAction VisitCommand(CommandAst commandAst)
    {
        var name = commandAst.GetCommandName();
        if (string.IsNullOrEmpty(name))
        {
            return AstVisitAction.Continue;
        }

        var command = cmdlet.InvokeCommand.GetCommand(name, CommandTypes.Application);
        if (command is ApplicationInfo appInfo)
        {
            NativeCommands.Add(new(commandAst, appInfo));
        }

        return AstVisitAction.Continue;
    }
}
