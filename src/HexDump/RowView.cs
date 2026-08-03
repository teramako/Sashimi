namespace Sashimi.HexDump;

/// <summary>
/// Abstract class that governs the view for the Show-HexDump
/// </summary>
public abstract class RowView(CharCollectionRow row)
{
    protected readonly CharCollectionRow _data = row;

    public CharData[] ListCharData => _data.RowData.Where(static c => c.Filled).ToArray();
}

/// <summary>
/// A view that displays hex values and character values in separate columns.
/// </summary>
public sealed class SplitView(CharCollectionRow row) : RowView(row)
{
    public string Offset => _data.Offset;
    public string Hex => _data.GetHexRow();
    public string Chars => _data.GetCharsRow();
}

/// <summary>
/// A view that displays the hex value on the first line
/// and the character value on the second line in a single column.
/// </summary>
public sealed class UnifiedView(CharCollectionRow row) : RowView(row)
{
    public string Offset => _data.Offset;
    public string HexAndChars => _data.GetHexAndCharsRow();
}
