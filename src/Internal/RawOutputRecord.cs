namespace Sashimi.Internal;

internal abstract record RawOutputRecord(RedirectTo To, OutputFrom From);

internal record ChunkOutput(byte[] Value, RedirectTo To, OutputFrom From)
    : RawOutputRecord(To, From)
{
    public override string ToString() => string.Join(':', Value.Select(b => b.ToString("X2")));
}

internal record StringOutput(string Value, RedirectTo To, OutputFrom From)
    : RawOutputRecord(To, From)
{
    public override string ToString() => Value;
}
