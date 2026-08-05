using System.Globalization;

namespace Sashimi.HexDump.Internal;

/// <summary>
/// Helper functions calculate Terminal Escape Sequences for color value
/// </summary>
internal static class Color
{
    public const string Reset = "\u001b[0m";

    /// <summary>
    /// Returns the escape sequences for an HLS-color terminal.
    /// </summary>
    /// <inheritdoc cref="RGB.FromHLS(int, int, int)"/>
    public static string GetFromHLS(int h, int l, int s)
    {
        return RGB.FromHLS(h, l, s).ToTermBg();
    }

    /// <summary>
    /// Returns the terminal escape sequence corresponding to a byte value.
    /// </summary>
    /// <param name="b">Byte value. Maps to the hue (H) of the HLS color. 0–255</param>
    /// <param name="config">Config</param>
    public static string GetColorFromByte(byte b, Config? config = null)
    {
        config ??= Config.Default;
        return GetFromHLS(config.InitialHue + (int)(b * 360.0 / 0xFF),
                          config.DefaultLightness,
                          config.DefaultSaturation);
    }

    /// <summary>
    /// Returns the terminal escape sequences corresponding to <see cref="CharData.Type"/> and <see cref="CharData.CodePoint"/>.
    /// <para>
    /// Return the results categorized as follows.
    /// <list type="bullet">
    ///     <item>
    ///         <term>Binary</term>
    ///         <description>Characters that could not be decoded</description>
    ///     </item>
    ///     <item>
    ///         <term>SingleByteChar</term>
    ///         <description>A single-byte character. Further color-coded based on whether it is a control character or a non-ASCII character</description>
    ///     </item>
    ///     <item>
    ///         <term>MultiByteChar</term>
    ///         <description>A multibyte character</description>
    ///     </item>
    ///     <item>
    ///         <term>Other</term>
    ///         <description>Generally uncolored</description>
    ///     </item>
    /// </list>
    /// </para>
    /// </summary>
    /// <param name="charType"></param>
    /// <param name="codePoint"></param>
    /// <param name="config">Config</param>
    public static string GetFromCharType(CharType charType, int codePoint, Config? config = null)
    {
        config ??= Config.Default;
        return charType switch
        {
            CharType.Binary /* data which failed decode to a char */
                => GetFromHLS(config.InitialHue,
                              config.DefaultLightness,
                              0), // Gray scale
            CharType.SingleByteChar => codePoint switch
            {
                < 0x20 or 0x7F /* ASCII Control chars */
                    => GetFromHLS(config.InitialHue,
                                  config.DefaultLightness,
                                  config.DefaultSaturation),
                < 0x7F /* ASCII chars */
                    => GetFromHLS(config.InitialHue + 90,
                                  config.DefaultLightness,
                                  config.DefaultSaturation),
                < 0xA0 /* Non-ASCII control chars */
                    => GetFromHLS(config.InitialHue + 10,
                                  config.DefaultLightness,
                                  config.DefaultSaturation),
                _ /* Non-ASCII chars */
                    => GetFromHLS(config.InitialHue + 120,
                                  config.DefaultLightness,
                                  config.DefaultSaturation),
            },
            CharType.MultiByteChar
                or CharType.ContinuationByte
                or CharType.ContinuationFirstByte
                or CharType.ContinuationFirstAndLastByte
                or CharType.ContinuationLastByte
                => GetFromHLS(config.InitialHue + 150,
                              config.DefaultLightness,
                              config.DefaultSaturation),
            _ => string.Empty
        };
    }

    /// <summary>
    /// <see cref="UnicodeCategory"/> Returns the corresponding terminal escape sequence from the value.
    /// <para>
    /// There are 30 <see cref="UnicodeCategory"/> entries, ranging from 0 to 29.
    /// </para>
    /// </summary>
    /// <param name="uc">Unicode Category</param>
    /// <param name="config">Config</param>
    public static string GetFromUnicodeCategory(UnicodeCategory? uc, Config? config = null)
    {
        config ??= Config.Default;
        const double NumberOfCategories = 30.0;
        return uc is null
            ? string.Empty
            : GetFromHLS(config.InitialHue + (int)Math.Ceiling((int)uc / NumberOfCategories * 360.0),
                         config.DefaultLightness,
                         config.DefaultSaturation);
    }
}
