using System.Globalization;
using System.Text;

namespace Heliograph.Tests.Support;

/// <summary>
/// The lists RandomLayout writes beyond a plain bullet or number list, three levels each, covering what the converter
/// claims for lists: letters, Roman numerals and leading zeros; labels that show higher levels ("1.2."); legal numbering;
/// labels aligned right; a space or nothing after a label; Word's Symbol, Courier New and Wingdings bullets; a list that
/// counts on from another with the same definition, and one that starts again.
/// </summary>
internal static class RandomLists
{
    private static readonly string[][] Levels =
    [
        // Outline: 1. (a) i.
        [Level("decimal", "%1."), Level("lowerLetter", "(%2)"), Level("lowerRoman", "%3.", jc: "right")],
        // Legal: I. then 1.1. and 1.1.1., shown as plain numbers.
        [Level("upperRoman", "%1."), Level("decimal", "%1.%2.", legal: true), Level("decimal", "%1.%2.%3.", legal: true)],
        // Word's bullets.
        [Level("bullet", "", font: "Symbol"), Level("bullet", "o", font: "Courier New"), Level("bullet", "", font: "Wingdings")],
        // A) 01 and a space or nothing after the label.
        [Level("upperLetter", "%1)", suffix: "space"), Level("decimalZero", "%2", suffix: "nothing"), Level("lowerLetter", "%3.")],
    ];

    /// <summary>
    /// The abstractNum elements, numbered from 2 after RandomLayout's own two. Every abstractNum must come before every num,
    /// as the format says: Word for the web, given them mixed, uses the last definition for every list.
    /// </summary>
    public static string Definitions
    {
        get
        {
            var xml = new StringBuilder();
            for (var a = 0; a < Levels.Length; a++)
            {
                var levels = string.Concat(Levels[a].Select((level, l) => level.Replace("{ilvl}", Number(l), StringComparison.Ordinal).Replace("{left}", Number(720 * (l + 1)), StringComparison.Ordinal)));
                xml.Append(CultureInfo.InvariantCulture, $"<w:abstractNum w:abstractNumId=\"{a + 2}\">{levels}</w:abstractNum>");
            }

            return xml.ToString();
        }
    }

    /// <summary>The num elements, numbered from 3 after RandomLayout's own two, each using its definition.</summary>
    public static string Lists
    {
        get
        {
            var xml = new StringBuilder();
            for (var a = 0; a < Levels.Length; a++)
            {
                xml.Append(CultureInfo.InvariantCulture, $"<w:num w:numId=\"{a + 3}\"><w:abstractNumId w:val=\"{a + 2}\"/></w:num>");
            }

            // 7 counts on from 3, sharing its definition; 8 starts the outline again at 1.
            xml.Append("<w:num w:numId=\"7\"><w:abstractNumId w:val=\"2\"/></w:num>");
            xml.Append("<w:num w:numId=\"8\"><w:abstractNumId w:val=\"2\"/><w:lvlOverride w:ilvl=\"0\"><w:startOverride w:val=\"1\"/></w:lvlOverride></w:num>");
            return xml.ToString();
        }
    }

    public static int Pick(Random random) => random.Next(3, 9);

    private static string Level(string format, string text, string jc = "left", bool legal = false, string? suffix = null, string? font = null) =>
        "<w:lvl w:ilvl=\"{ilvl}\"><w:start w:val=\"1\"/>"
        + $"<w:numFmt w:val=\"{format}\"/>{(legal ? "<w:isLgl/>" : "")}{(suffix is null ? "" : $"<w:suff w:val=\"{suffix}\"/>")}"
        + $"<w:lvlText w:val=\"{text}\"/><w:lvlJc w:val=\"{jc}\"/><w:pPr><w:ind w:left=\"{{left}}\" w:hanging=\"360\"/></w:pPr>"
        + (font is null ? "" : $"<w:rPr><w:rFonts w:ascii=\"{font}\" w:hAnsi=\"{font}\" w:hint=\"default\"/></w:rPr>")
        + "</w:lvl>";

    private static string Number(int n) => n.ToString(CultureInfo.InvariantCulture);
}
