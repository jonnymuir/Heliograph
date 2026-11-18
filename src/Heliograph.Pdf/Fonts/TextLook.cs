using Heliograph.Pdf.Reading;

namespace Heliograph.Pdf.Fonts;

/// <summary>
/// How a run's text is drawn: superscript and subscript at 58% of the size, raised until its top meets the full-size
/// text's or lowered until its foot meets the full-size descent, as LibreOffice sets them, so a line is as tall as it
/// would be without them; and the space added after each character. Every width the layout uses comes from here.
/// </summary>
internal static class TextLook
{
    private const double Reduced = 0.58;

    /// <summary>The size the glyphs are drawn at.</summary>
    public static double Size(RunStyle style) => style.VertAlign is "superscript" or "subscript" ? style.Size * Reduced : style.Size;

    /// <summary>How far above the baseline the glyphs sit; below it is negative.</summary>
    public static double Rise(RunStyle style)
    {
        var face = FontLibrary.Face(style);
        var unshrunk = style.Size * (1 - Reduced) / face.UnitsPerEm;
        return style.VertAlign switch
        {
            "superscript" => face.Ascender * unshrunk,
            "subscript" => -face.WinDescent * unshrunk,
            _ => 0,
        };
    }

    /// <summary>The width of a run of text in its style, spacing included.</summary>
    public static double Width(string text, RunStyle style) =>
        FontLibrary.Face(style).Width(text, Size(style)) + (style.CharacterSpacing * text.EnumerateRunes().Count());
}
