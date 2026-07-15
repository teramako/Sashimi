using System.Collections.ObjectModel;
using System.Management.Automation.Language;

namespace Sashimi.Internal;

internal readonly record struct Redirection(RedirectTo StdoutTo, RedirectTo StderrTo)
{
    private Redirection((RedirectTo StdoutTo, RedirectTo StderrTo) t) : this(t.StdoutTo, t.StderrTo)
    { }

    public Redirection(OutputFrom outputFrom) : this(outputFrom switch
    {
        OutputFrom.Both => (RedirectTo.Output, RedirectTo.Output),
        OutputFrom.Stderr => (RedirectTo.Null, RedirectTo.Output),
        OutputFrom.Stdout or _ => (RedirectTo.Output, RedirectTo.Error),
    })
    { }

    public static readonly Redirection Default = new(RedirectTo.Output, RedirectTo.Error);

    public static Redirection GetRedirectionFromStatement(string? statement, OutputFrom outputFrom)
    {
        if (string.IsNullOrEmpty(statement))
            return new(outputFrom);

        var ast = Parser.ParseInput(statement, out _, out _);
        return ParseRedirections(GetRedirections(ast), new(outputFrom));
    }

    private static ReadOnlyCollection<RedirectionAst> GetRedirections(ScriptBlockAst ast)
    {
        var pipelineAst = ast.EndBlock.Statements[0] as PipelineAst;
        if (pipelineAst is null)
            return [];

        var commandAst = pipelineAst.PipelineElements[0];
        return commandAst.Redirections;
        // return commandAst.Redirections.OrderBy(ast => ast.Extent.StartOffset).ToArray();
    }

    private static Redirection ParseRedirections(ReadOnlyCollection<RedirectionAst> redirections, Redirection initialRedirection)
    {
        if (redirections.Count == 0)
            return initialRedirection;

        (RedirectTo stdout, RedirectTo stderr) = initialRedirection;

        foreach (var redirectionAst in redirections)
        {
            switch (redirectionAst)
            {
                case MergingRedirectionAst mergingRedirectionAst:
                    switch (mergingRedirectionAst.FromStream)
                    {
                        case RedirectionStream.All: // `*>&n`
                            stdout = stderr = (RedirectTo)mergingRedirectionAst.ToStream;
                            break;
                        case RedirectionStream.Output: // `>&n`
                            if (mergingRedirectionAst.ToStream is RedirectionStream.Error)
                            {
                                // `>&2`
                                stdout = stderr;
                            }
                            else
                            {
                                // Since this isn't implemented in PowerShell itself, probably won't ever end up here
                                stdout = (RedirectTo)mergingRedirectionAst.ToStream;
                            }
                            break;
                        case RedirectionStream.Error: // `2>&n`
                            if (mergingRedirectionAst.ToStream is RedirectionStream.Output)
                            {
                                // `2>&1`
                                stderr = stdout;
                            }
                            else
                            {
                                // Since this isn't implemented in PowerShell itself, probably won't ever end up here
                                stderr = (RedirectTo)mergingRedirectionAst.ToStream;
                            }
                            break;
                    }
                    break;
                case FileRedirectionAst fileRedirectionAst:
                    if (fileRedirectionAst.Location is VariableExpressionAst exp)
                    {
                        if (!exp.VariablePath.UserPath.Equals("null", StringComparison.OrdinalIgnoreCase))
                            break;

                        switch (fileRedirectionAst.FromStream)
                        {
                            case RedirectionStream.All: // `*>$null`
                                stdout = stderr = RedirectTo.Null;
                                break;
                            case RedirectionStream.Output: // `>$null`
                                stdout = RedirectTo.Null;
                                break;
                            case RedirectionStream.Error: // `2>$null`
                                stderr = RedirectTo.Null;
                                break;
                            default:
                                break;
                        }
                    }
                    break;
            }
        }

        return new(stdout, stderr);
    }
}
