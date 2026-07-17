using System.Management.Automation;
using System.Management.Automation.Language;
using System.Text;

namespace Sashimi.Internal;

internal sealed class ScriptBlockExecutionEngine : ExecutionEngine
{
    private ScriptBlockAst _ast;
    private NativeCommandFinder _finder;
    private IDictionary<string, object?>? _forwardParameters;

    public ScriptBlockExecutionEngine(InvokeRawCommandCommand cmdlet,
                                      ScriptBlock scriptBlock,
                                      IDictionary<string, object?>? forwardParameters = null)
        : base(cmdlet)
    {
        var scriptBlockAst = scriptBlock.Ast as ScriptBlockAst;
        ArgumentNullException.ThrowIfNull(scriptBlockAst);
        _ast = scriptBlockAst;
        _forwardParameters = forwardParameters;

        _finder = new NativeCommandFinder(cmdlet);
    }

    public override void BeginProcessing() => _ast.Visit(_finder);

    public override void EndProcessing() => ExecuteScriptBlock();

    private void ExecuteScriptBlock()
    {
        var script = GetReplacedScript();
        var rawScriptBlock = ScriptBlock.Create(script);
        WriteVerboseRaw($"Invoke {{\n{rawScriptBlock}\n}}");
        try
        {
            var results = rawScriptBlock.InvokeWithContext(null, null);
            WriteVerboseRaw($"Result: {results.Count}");
            WriteObject(results, true);
        }
        catch(Exception ex)
        {
            throw new InvalidOperationException($"Failed to invoke script: {script}", ex);
        }
    }

    private string GetReplacedScript()
    {
        StringBuilder script = new();

        ReadOnlySpan<char> original = _ast.EndBlock.Extent.Text;

        string cmdletOptions = ParametersToString(_forwardParameters);
        int start = _ast.EndBlock.Extent.StartOffset;
        int offset = start;
        foreach ((var commandAst, var appInfo) in _finder.NativeCommands)
        {
            if (commandAst.CommandElements.Count == 0)
                continue;

            var cmdAst = commandAst.CommandElements[0];

            script.Append(original[(offset - start)..(cmdAst.Extent.StartOffset - start)]);

            script.Append($"{Cmdlet.MyCommandName} {cmdletOptions} '{CodeGeneration.EscapeSingleQuotedStringContent(appInfo.Path)}'");
            if (commandAst.CommandElements.Count > 1)
            {
                script.Append(" --");
            }

            offset = cmdAst.Extent.EndOffset;
        }

        if (original.Length >= offset - start)
        {
            script.Append(original[(offset - start)..]);
        }

        return script.ToString();
    }

    private static string ParametersToString(IDictionary<string, object?>? parameters)
    {
        if (parameters is null || parameters.Count == 0)
            return string.Empty;

        return string.Join(' ', parameters.Select(kv => kv.Value switch
        {
            null => $"-{kv.Key}",
            SwitchParameter sw => $"-{kv.Key}{(sw.ToBool() ? "" : ":$false")}",
            _ => $"-{kv.Key} {kv.Value}"
        }));
    }
}
