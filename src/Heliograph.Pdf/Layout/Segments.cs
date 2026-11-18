using System.Text;
using Heliograph.Pdf.Fonts;
using Heliograph.Pdf.Reading;

namespace Heliograph.Pdf.Layout;

/// <summary>A character none of the built-in fonts can draw, found as a letter is laid out.</summary>
internal sealed class UndrawableCharacterException() : Exception("The letter has a character none of the built-in fonts can draw.");

/// <summary>
/// A piece of text split where its font lacks a character, each part drawn in a built-in font that has it: the run's own
/// first, then Liberation Sans, Serif and Mono, Carlito and Caladea.
/// </summary>
internal static class Segments
{
    public static Piece Piece(string text, RunStyle style, PieceKind kind)
    {
        var parts = new List<(string Text, RunStyle Style)>();
        var current = new StringBuilder();
        RunStyle? font = null;
        foreach (var rune in text.EnumerateRunes())
        {
            var covering = FontLibrary.Covering(style, rune.Value) ?? throw new UndrawableCharacterException();
            if (font is not null && covering != font)
            {
                parts.Add((current.ToString(), font));
                current.Clear();
            }

            font = covering;
            current.Append(rune.ToString());
        }

        if (font is not null)
        {
            parts.Add((current.ToString(), font));
        }

        var width = parts.Sum(p => TextLook.Width(p.Text, p.Style));
        return parts is [var only] && only.Style == style
            ? new Piece(text, style, width, kind)
            : new Piece(text, style, width, kind) { Segments = parts };
    }
}
