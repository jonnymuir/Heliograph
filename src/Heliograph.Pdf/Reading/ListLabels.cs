using System.Globalization;
using System.Text;

namespace Heliograph.Pdf.Reading;

/// <summary>
/// How a list label is written: a number in each of Word's common styles, and the bullet characters Word puts in
/// Symbol and Wingdings, which are not built in, as the same shapes in ordinary characters.
/// </summary>
internal static class ListLabels
{
    /// <summary>The number styles the converter writes. Any other is refused at compile.</summary>
    public static readonly IReadOnlySet<string> Formats = new HashSet<string>(StringComparer.Ordinal)
    {
        "decimal", "decimalZero", "lowerLetter", "upperLetter", "lowerRoman", "upperRoman", "bullet", "none",
    };

    // Symbol and Wingdings put their shapes at private code points (U+F0xx) or at the low byte itself.
    private static readonly Dictionary<int, char> SymbolFont = new() { [0xB7] = '•' };

    private static readonly Dictionary<int, char> Wingdings = new()
    {
        [0x6C] = '●', [0x6E] = '■', [0x71] = '❑', [0x76] = '❖', [0xA7] = '▪', [0xD8] = '➢', [0xFC] = '✓',
    };

    private static readonly (int Value, string Numeral)[] Romans =
        [(1000, "m"), (900, "cm"), (500, "d"), (400, "cd"), (100, "c"), (90, "xc"), (50, "l"), (40, "xl"), (10, "x"), (9, "ix"), (5, "v"), (4, "iv"), (1, "i")];

    /// <summary>A number in a style: 3 as "3", "03", "c", "C", "iii" or "III"; nothing for "none".</summary>
    public static string Number(int n, string format) => format switch
    {
        "decimalZero" => n.ToString("00", CultureInfo.InvariantCulture),
        "lowerLetter" => Letters(n),
        "upperLetter" => Letters(n).ToUpperInvariant(),
        "lowerRoman" => Roman(n),
        "upperRoman" => Roman(n).ToUpperInvariant(),
        "none" or "bullet" => "",
        _ => n.ToString(CultureInfo.InvariantCulture),
    };

    /// <summary>
    /// A bullet as text to draw, and whether it is drawn in its own font (false for Symbol and Wingdings, whose shapes
    /// are drawn from the text's font instead).
    /// </summary>
    public static (string Text, bool OwnFont) Bullet(string text, string font)
    {
        var table = font.ToUpperInvariant() switch
        {
            "SYMBOL" => SymbolFont,
            "WINGDINGS" => Wingdings,
            _ => null,
        };
        if (table is null)
        {
            return (text, true);
        }

        var mapped = new StringBuilder();
        foreach (var c in text)
        {
            var code = c >= 0xF000 && c <= 0xF0FF ? c - 0xF000 : c;
            mapped.Append(table.TryGetValue(code, out var shape) ? shape : c);
        }

        return (mapped.ToString(), false);
    }

    // Word's letters run a to z, then aa to zz, then aaa: the letter repeated, not counted on.
    private static string Letters(int n)
    {
        if (n < 1)
        {
            return n.ToString(CultureInfo.InvariantCulture);
        }

        return new string((char)('a' + ((n - 1) % 26)), ((n - 1) / 26) + 1);
    }

    private static string Roman(int n)
    {
        if (n is < 1 or > 3999)
        {
            return n.ToString(CultureInfo.InvariantCulture);
        }

        var text = new StringBuilder();
        foreach (var (value, numeral) in Romans)
        {
            for (; n >= value; n -= value)
            {
                text.Append(numeral);
            }
        }

        return text.ToString();
    }
}
