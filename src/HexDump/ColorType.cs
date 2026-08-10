namespace Sashimi.HexDump;

/// <summary>
/// Color scheme for dump results
/// </summary>
public enum ColorType
{
    /// <summary>
    /// No color
    /// </summary>
    None = 0,

    /// <summary>
    /// Color scheme based on byte values
    /// </summary>
    ByByte = 1,

    /// <summary>
    /// Color scheme based on <see cref="CharData.Type"/>
    /// </summary>
    ByCharType = 2,

    /// <summary>
    /// Color scheme based on <see cref="CharData.UnicodeCategory"/>
    /// </summary>
    ByUnicodeCategory = 3
}
