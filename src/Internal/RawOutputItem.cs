namespace Sashimi.Internal;

internal abstract record RawOutputItem(OutputType To);

internal record ChunkOutput(byte[] Value, OutputType To) : RawOutputItem(To);

internal record StringOutput(string Value, OutputType To) : RawOutputItem(To);
