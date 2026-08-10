using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;
using Sashimi.HexDump.Internal;

namespace Sashimi.HexDump;

/// <summary>
/// Represents character data as byte values.
/// For multibyte characters, the character is represented by its Unicode code point (UTF-32).
/// (Only the first byte value is represented as a character.)
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 4)]
public struct CharData(byte b, Rune rune, CharType type)
{
    /// <summary>
    /// Unicode Runes
    /// </summary>
    public readonly Rune Rune = rune;

    /// <summary>
    /// Byte data
    /// </summary>
    public readonly byte B = b;

    /// <summary>
    /// Represents the type of a byte value.
    /// <list type="table">
    ///     <listheader><term>Name</term><description>Description</description></listheader>
    ///     <item>
    ///         <term><see cref="CharType.Empty"/></term>
    ///         <description>Unassigned. Not rendered as text.</description>
    ///     </item>
    ///     <item>
    ///         <term><see cref="CharType.Binary"/></term>
    ///         <description>A byte value that could not be decoded. It will not be rendered as a character.</description>
    ///     </item>
    ///     <item>
    ///         <term><see cref="CharType.SingleByteChar"/></term>
    ///         <description>Singile byte character</description>
    ///     </item>
    ///     <item>
    ///         <term><see cref="CharType.MultiByteChar"/></term>
    ///         <description>Multibyte characters</description>
    ///     </item>
    ///     <item>
    ///         <term><see cref="CharType.ContinuationByte"/></term>
    ///         <description>The subsequent byte of a multibyte character. It is not rendered as a character.</description>
    ///     </item>
    ///     <item>
    ///         <term><see cref="CharType.ContinuationFirstByte"/></term>
    ///         <description>The first byte of the sequence following a multibyte character. It is not rendered as a character.</description>
    ///     </item>
    ///     <item>
    ///         <term><see cref="CharType.ContinuationFirstAndLastByte"/></term>
    ///         <description>The first and last bytes following a multibyte character (which corresponds to the second byte of a 2-byte character).
    ///         These are not rendered as characters.</description>
    ///     </item>
    ///     <item>
    ///         <term><see cref="CharType.ContinuationLastByte"/></term>
    ///         <description>The last byte of a multibyte character. It is not rendered as a character.</description>
    ///     </item>
    /// </list>
    /// </summary>
    public readonly CharType Type = type;

    /// <summary>
    /// Unicode Code Point
    /// </summary>
    public int CodePoint => Rune.Value;

    /// <summary>
    /// A flag indicating that this structure contains byte data, a position, and a value that specifies a codepoint (not the default value)
    /// </summary>
    internal bool Filled => Type is not CharType.Empty;

    /// <summary>
    /// Indicates whether the data is a character.
    /// (Data representing subsequent bytes of a multibyte character is excluded.)
    /// </summary>
    public bool IsChar => Type.HasFlag(CharType.Char);

    /// <summary>
    /// Constructor for specifying Unicode code points
    /// </summary>
    /// <param name="b">The byte data for that point</param>
    /// <param name="codePoint">Unicode code point. </param>
    /// <param name="type">Character type of the byte value</param>
    public CharData(byte b, int codePoint, CharType type)
        : this(b, new Rune(codePoint), type)
    { }

    /// <summary>
    /// A value obtained by simply converting a code point to a string
    /// </summary>
    public string RawString => Filled ? Rune.ToString()  : string.Empty;

    public UnicodeCategory? UnicodeCategory => Filled ? Rune.GetUnicodeCategory(Rune) : null;

    /// <summary>
    /// Returns a string for displaying the dump results
    /// </summary>
    public string DisplayString => IsChar
        ? Type switch
        {
            CharType.SingleByteChar => Rune.Value switch
            {
                < 0x20 => $"^{(char)(Rune.Value + 0x40)}",
                < 0x7F => $"{Rune}",
                0x7F => $"^{(char)(Rune.Value - 0x40)}",
                < 0xA0 => $"^{(char)(Rune.Value + 0x40)}",
                _ => Rune.ToString()
            },
            CharType.MultiByteChar => Rune.ToString(),
            _ => string.Empty
        }
        : string.Empty;

    /// <summary>
    /// Write the string used to display the dump results to <paramref name="sb"/>
    /// </summary>
    /// <param name="sb">An instance of <see cref="StringBuilder"/> to which values will be added </param>
    /// <param name="config">Various configuration values</param>
    /// <param name="cellLength">Number of cells. If there are not enough cells, half-width spaces will be added at the end </param>
    internal void PrintDisplayString(StringBuilder sb, Config config, int cellLength)
    {
        var str = Type switch
        {
            CharType.Empty => new string(config.NullLetter, cellLength),
            CharType.Binary => new string(config.NonLetter, cellLength),
            _ => Rune.Value switch
            {
                < 0x20 => $"{config.AsciiControlLetters[Rune.Value]}",
                < 0x7F => $"{Rune}",
                0x7F => $"{config.AsciiControlLetters[0x21]}",
                < 0xA0 => $"^{(char)(Rune.Value + 0x40)}",
                _ => Rune.ToString()
            },
        };
        var strCellLen = LengthInBufferCells(str);
        switch (Type)
        {
            case CharType.MultiByteChar when cellLength > strCellLen:
                sb.Append($"{str}{config.ContinuationLetters[0]}")
                  .Append(config.ContinuationLetters[1], cellLength - strCellLen - 1);
                break;
            case CharType.ContinuationFirstByte:
                if (cellLength > strCellLen)
                {
                    sb.Append(config.ContinuationLetters[1], cellLength);
                }
                else
                {
                    sb.Append(config.ContinuationLetters[0])
                      .Append(config.ContinuationLetters[1], cellLength - 1);
                }
                break;
            case CharType.ContinuationFirstAndLastByte:
                if (cellLength > strCellLen)
                {
                    sb.Append(config.ContinuationLetters[1], cellLength - 1)
                      .Append(config.ContinuationLetters[2]);
                }
                else
                {
                    sb.Append(config.ContinuationLetters[0])
                      .Append(config.ContinuationLetters[1], cellLength - 2)
                      .Append(config.ContinuationLetters[2]);
                }
                break;
            case CharType.ContinuationLastByte:
                sb.Append(config.ContinuationLetters[1], cellLength -1)
                  .Append(config.ContinuationLetters[2]);
                break;
            case CharType.ContinuationByte:
                sb.Append(config.ContinuationLetters[1], cellLength);
                break;
            default:
                sb.Append(str);
                if (strCellLen < cellLength)
                {
                    sb.Append(Type switch
                    {
                        CharType.SingleByteChar => ' ',
                        _ => str[^1]
                    }, cellLength - strCellLen);
                }
                break;
        }
    }

    /// <summary>
    /// Calculates the number of cells in the target string. (Equivalent to how many half-width characters it occupies in the terminal.)
    /// </summary>
    internal static int LengthInBufferCells(string str)
    {
        return str.Sum(LengthInBufferCells);
    }

    /// <summary>
    /// Calculate the number of cells occupied by the target text. (How many half-width characters does it take up in the terminal?)
    /// </summary>
    /// <seealso href="https://github.com/PowerShell/PowerShell/blob/7fe5cb3e354eb775778944e5419cfbcb8fede735/src/Microsoft.PowerShell.ConsoleHost/host/msh/ConsoleControl.cs#L2785-L2806"/>
    internal static int LengthInBufferCells(char c)
    {
        // The following is based on http://www.cl.cam.ac.uk/~mgk25/c/wcwidth.c
        // which is derived from https://www.unicode.org/Public/UCD/latest/ucd/EastAsianWidth.txt
        bool isWide = c >= 0x1100 &&
            (c <= 0x115f || /* Hangul Jamo init. consonants */
             c == 0x2329 || c == 0x232a ||
             ((uint)(c - 0x2e80) <= (0xa4cf - 0x2e80) &&
              c != 0x303f) || /* CJK ... Yi */
             ((uint)(c - 0xac00) <= (0xd7a3 - 0xac00)) || /* Hangul Syllables */
             ((uint)(c - 0xf900) <= (0xfaff - 0xf900)) || /* CJK Compatibility Ideographs */
             ((uint)(c - 0xfe10) <= (0xfe19 - 0xfe10)) || /* Vertical forms */
             ((uint)(c - 0xfe30) <= (0xfe6f - 0xfe30)) || /* CJK Compatibility Forms */
             ((uint)(c - 0xff00) <= (0xff60 - 0xff00)) || /* Fullwidth Forms */
             ((uint)(c - 0xffe0) <= (0xffe6 - 0xffe0)));

        // We can ignore these ranges because .Net strings use surrogate pairs
        // for this range and we do not handle surrogate pairs.
        // (c >= 0x20000 && c <= 0x2fffd) ||
        // (c >= 0x30000 && c <= 0x3fffd)
        return 1 + (isWide ? 1 : 0);
    }

    /// <summary>
    /// Writes the color (escape sequence) specified by <paramref name="colorType"/> to <paramref name="sb"/>
    /// </summary>
    /// <param name="sb">StringBuilder</param>
    /// <param name="config">Various configuration values</param>
    internal void PrintColor(StringBuilder sb, Config config)
    {
        var escapeSequence = config.ColorType switch
        {
            ColorType.ByByte => Color.GetColorFromByte(B, config),
            ColorType.ByCharType => Color.GetFromCharType(Type, CodePoint, config),
            ColorType.ByUnicodeCategory => Color.GetFromUnicodeCategory(UnicodeCategory, config),
            _ => string.Empty
        };
        if (string.IsNullOrEmpty(escapeSequence))
            return;

        sb.Append(escapeSequence);
    }

    public override string ToString()
    {
        return !Filled
            ? "<Empty>"
            : IsChar
              ? $"Byte: 0x{B:X2} CodePoint: U+{Rune.Value:X8} <{UnicodeCategory}> {DisplayString}"
              : $"Byte: 0x{B:X2}";
    }
}

