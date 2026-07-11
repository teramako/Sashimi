using System.Management.Automation;

namespace Sashimi.Internal;

internal abstract record RawOutputItem;

internal record ChunkOutput(byte[] Value) : RawOutputItem;

internal record StringOutput(string Value) : RawOutputItem;

internal record InformationOutput(InformationRecord Value) : RawOutputItem;
