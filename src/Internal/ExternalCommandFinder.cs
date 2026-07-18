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
    public readonly record struct ExternalCommandItem(CommandAst Ast, ApplicationInfo AppInfo, int PipelinePosition = 0);

    public List<List<ExternalCommandItem>> ExternalCommandChains = [];

    public override AstVisitAction VisitPipeline(PipelineAst pipelineAst)
    {
        List<ExternalCommandItem> chain = [];
        int lastPosition = -1;
        foreach (var item in GetExternalCommands(pipelineAst))
        {
            if (item.PipelinePosition != lastPosition + 1 && chain.Count > 0)
            {
                ExternalCommandChains.Add(chain);
                chain = [];
            }
            chain.Add(item);
            lastPosition = item.PipelinePosition;
        }

        if (chain.Count > 0)
        {
            ExternalCommandChains.Add(chain);
        }

        return AstVisitAction.Continue;
    }

    private IEnumerable<ExternalCommandItem> GetExternalCommands(PipelineAst pipelineAst)
    {
        for (var i = 0; i < pipelineAst.PipelineElements.Count; i++)
        {
            var element = pipelineAst.PipelineElements[i];
            if (element is CommandAst cmdAst)
            {
                var name = cmdAst.GetCommandName();
                if (!TryGetApplicationInfo(name, out var appInfo))
                {
                    continue;
                }
                yield return new(cmdAst, appInfo, i);
            }
        }
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
