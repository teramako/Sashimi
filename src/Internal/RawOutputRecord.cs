namespace Sashimi.Internal;

internal abstract record RawOutputRecord(RedirectTo To);

internal record ChunkOutput(byte[] Value, RedirectTo To) : RawOutputRecord(To)
{
    public override string ToString() => string.Join(':', Value.Select(b => b.ToString("X2")));
}

internal record StringOutput(string Value, RedirectTo To) : RawOutputRecord(To)
{
    public override string ToString() => Value;
}
