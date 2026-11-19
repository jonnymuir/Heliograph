using System.Text;
using System.Text.RegularExpressions;
using Heliograph.Pdf.Fonts;
using Heliograph.Pdf.Layout;
using static Heliograph.Pdf.Writing.PdfObjects;

namespace Heliograph.Pdf.Writing;

/// <summary>
/// A run's underline as PDF drawing: below the baseline in its own colour or the text's, one line, two for double, a
/// heavier one for thick, and none under the spaces when only the words are underlined.
/// </summary>
internal static partial class Underlines
{
    public static string Of(TextOp text, double width, double thickness, double pageHeight)
    {
        var style = text.Style;
        if (!style.Underline)
        {
            return "";
        }

        var y = pageHeight - text.Baseline - (style.Size / 8);
        var (weight, ys) = style.UnderlineStyle switch
        {
            "double" => (thickness, new[] { y, y - (thickness * 2.5) }),
            "thick" => (thickness * 2, new[] { y }),
            _ => (thickness, new[] { y }),
        };
        var drawing = new StringBuilder();
        foreach (var (from, to) in Spans(text, width))
        {
            foreach (var at in ys)
            {
                drawing.Append(Invariant($"{Rgb(style.UnderlineColor ?? style.Color)} RG {N(weight)} w {N(from)} {N(at)} m {N(to)} {N(at)} l S\n"));
            }
        }

        return drawing.ToString();
    }

    // Where an underline runs across a piece of text: all of it, or under each word only, leaving out the spaces.
    private static IEnumerable<(double From, double To)> Spans(TextOp text, double width)
    {
        if (text.Style.UnderlineStyle != "words")
        {
            yield return (text.X, text.X + width);
            yield break;
        }

        foreach (Match word in Word().Matches(text.Text))
        {
            var before = TextLook.Width(text.Text[..word.Index], text.Style);
            yield return (text.X + before, text.X + before + TextLook.Width(word.Value, text.Style));
        }
    }

    // An RGB hex colour as PDF colour components; automatic is black.
    private static string Rgb(string? hex)
    {
        var value = Convert.ToInt32(hex ?? "000000", 16);
        return Invariant($"{N(((value >> 16) & 0xFF) / 255.0)} {N(((value >> 8) & 0xFF) / 255.0)} {N((value & 0xFF) / 255.0)}");
    }

    [GeneratedRegex(@"\S+")]
    private static partial Regex Word();
}
