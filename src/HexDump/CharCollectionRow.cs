using System.Text;
using Sashimi.HexDump.Internal;

namespace Sashimi.HexDump;

/// <summary>
/// <see cref="CharData"/> A line containing 16 characters
/// </summary>
public class CharCollectionRow(long row, Config config)
{
    internal CharData[] RowData = new CharData[16];
    private long _offset = row & 0x7FFFFFF0;

    /// <summary>
    /// A flag indicating whether at least one <see cref="CharData"/> element has been set
    /// </summary>
    internal bool IsEmpty = true;

    internal void Set(long position, CharData data)
    {
        RowData[position & 0x0F] = data;
        IsEmpty = false;
    }

    public IEnumerator<CharData> GetEnumerator()
    {
        return RowData.Where(static c => c.Filled).GetEnumerator();
    }

    public string Offset => $"0x{_offset:X8}";

    /// <summary>
    /// Various configuration values
    /// </summary>
    public Config Config { get; set; } = config;

    public string Hex => GetHexRow();

    /// <remarks>
    /// The color scheme is determined by the <see cref="ColorType"/> element configured for this instance.
    /// </remarks>
    /// <inheritdoc cref="GetHexRow(Config, int)"/>
    public string GetHexRow(int cellLength = 3)
    {
        return GetHexRow(Config, cellLength);
    }
    /// <summary>
    /// Returns a line of hexadecimal values for the byte data.
    /// </summary>
    /// <inheritdoc cref="PrintHexRow(StringBuilder, Config, int)"/>
    public string GetHexRow(Config config, int cellLength)
    {
        StringBuilder sb = StringBuilderPool.Rent(RowData.Length * (cellLength + config.HexColumnSeparator.Length));
        PrintHexRow(sb, config, cellLength);
        var result = sb.ToString();
        StringBuilderPool.Return(sb);
        return result;
    }

    /// <summary>
    /// Write the hexadecimal values of each byte of data to <paramref name="sb"/>
    /// </summary>
    /// <param name="sb">An instance of <see cref="StringBuilder"/> to which values will be added </param>
    /// <param name="config">Various configuration values</param>
    /// <param name="cellLength">Number of cells per data set</param>
    private void PrintHexRow(StringBuilder sb, Config config, int cellLength)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(cellLength, 2, nameof(cellLength));
        var remainingCellCount = cellLength - 2;
        for (var i = 0; i < RowData.Length; i++)
        {
            CharData c = RowData[i];
            if (i != 0)
            {
                if (config.ColorType is not ColorType.None)
                    sb.Append(Color.Reset);
                sb.Append(config.HexColumnSeparator);
            }
            if (c.Filled)
            {
                c.PrintColor(sb, config);
                sb.Append($"{c.B:X2}")
                  .Append(' ', remainingCellCount);
            }
            else
            {
                sb.Append(' ', cellLength);
            }
        }
    }

    public string Chars => GetCharsRow();

    /// <remarks>
    /// The color scheme is determined by the <see cref="ColorType"/> element configured for this instance.
    /// </remarks>
    /// <inheritdoc cref="GetCharsRow(Config, int)"/>
    public string GetCharsRow(int cellLength = 2)
    {
        return GetCharsRow(Config, cellLength);
    }
    /// <summary>
    /// Returns a line of text to be displayed.
    /// </summary>
    /// <inheritdoc cref="PrintCharsRow(StringBuilder, Config, int)"/>
    public string GetCharsRow(Config config, int cellLength)
    {
        StringBuilder sb = StringBuilderPool.Rent(RowData.Length * (cellLength + config.CharColumnSeparator.Length));
        PrintCharsRow(sb, config, cellLength);
        var result = sb.ToString();
        StringBuilderPool.Return(sb);
        return result;
    }

    /// <summary>
    /// Add the string representation of each byte of data to <paramref name="sb"/>
    /// </summary>
    /// <param name="sb">An instance of <see cref="StringBuilder"/> to which values will be added </param>
    /// <param name="cellLength">Number of cells per data set</param>
    private void PrintCharsRow(StringBuilder sb, Config config, int cellLength)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(cellLength, 2, nameof(cellLength));
        for (var i = 0; i < RowData.Length; i++)
        {
            CharData c = RowData[i];
            if (i != 0)
            {
                if (config.ColorType is not ColorType.None)
                    sb.Append(Color.Reset);
                sb.Append(config.CharColumnSeparator);
            }
            if (c.Filled)
            {
                c.PrintColor(sb, config);
                c.PrintDisplayString(sb, config, cellLength);
            }
            else
            {
                sb.Append(' ', cellLength);
            }
        }
    }

    /// <remarks>
    /// The color scheme is determined by the <see cref="ColorType"/> element configured for this instance.
    /// </remarks>
    /// <inheritdoc cref="GetHexAndCharsRow(Config, int)"/>
    public string GetHexAndCharsRow(int cellLength = 3)
    {
        return GetHexAndCharsRow(Config, cellLength);
    }
    /// <summary>
    /// Returns two lines—one for the Hex row and one for the Chars row—from each <see cref="RowData"/>, separated by a line break.
    /// </summary>
    /// <param name="cellLength">Number of cells per data set</param>
    public string GetHexAndCharsRow(Config config, int cellLength)
    {
        (int hexSepLen, int charSepLen) = (config.HexColumnSeparator.Length, config.CharColumnSeparator.Length);
        int maxSepLen = Math.Max(hexSepLen, charSepLen);
        int hexCellLen = (maxSepLen + cellLength) - hexSepLen;
        int charCellLen = (maxSepLen + cellLength) - charSepLen;

        StringBuilder sb = StringBuilderPool.Rent(RowData.Length * (cellLength + maxSepLen) * 2);
        PrintHexRow(sb, config, hexCellLen);
        sb.AppendLine();
        PrintCharsRow(sb, config, charCellLen);
        var result = sb.ToString();
        StringBuilderPool.Return(sb);
        return result;
    }

    public int Count => IsEmpty ? 0 : RowData.Count(static c => c.Filled);
}
