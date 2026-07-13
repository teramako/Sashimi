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
}
