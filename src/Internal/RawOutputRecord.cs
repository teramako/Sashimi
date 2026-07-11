namespace Sashimi.Internal;

internal abstract record RawOutputRecord(OutputType To);

internal record ChunkOutput(byte[] Value, OutputType To) : RawOutputRecord(To)
{
    public override string ToString() => string.Join(':', Value.Select(b => b.ToString("X2")));
}

internal record StringOutput(string Value, OutputType To) : RawOutputRecord(To)
{
    public override string ToString() => Value;
}
