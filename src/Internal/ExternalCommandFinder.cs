using System.Diagnostics.CodeAnalysis;
using System.Management.Automation;
using System.Management.Automation.Language;

namespace Sashimi.Internal;

/// <summary>
/// A <see cref="AstVisitor"/> that scans the ScriptBlock's AST and collects
/// command names that can be resolved as external commands (<see cref="ApplicationInfo"/>).
/// </summary>
internal sealed class ExternalCommandFinder(PSCmdlet cmdlet) : AstVisitor
{
    public readonly record struct ExternalCommandItem(CommandAst Ast, ApplicationInfo AppInfo);

    public List<ExternalCommandItem> ExternalCommands { get; } = [];

    public override AstVisitAction VisitCommand(CommandAst commandAst)
    {
        if (TryGetApplicationInfo(commandAst.GetCommandName(), out var appInfo))
        {
            ExternalCommands.Add(new(commandAst, appInfo));
        }
        return AstVisitAction.Continue;
    }

    private readonly Dictionary<string, ApplicationInfo> _appInfoCache = [];

    private bool TryGetApplicationInfo(string? name,
                                       [MaybeNullWhen(false)] out ApplicationInfo appInfo)
    {
        appInfo = null;
        if (string.IsNullOrEmpty(name))
            return false;

        if (_appInfoCache.TryGetValue(name, out appInfo))
            return true;

        var command = cmdlet.InvokeCommand.GetCommand(name, CommandTypes.Application);
        if (command is ApplicationInfo appInfo2)
        {
            _appInfoCache.Add(name, appInfo2);
            appInfo = appInfo2;
            return true;
        }
        return false;
    }
}
