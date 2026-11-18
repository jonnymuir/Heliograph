using Heliograph.Documents;
using Heliograph.Pdf.Fonts;
using Heliograph.Pdf.Reading;
using static Heliograph.Pdf.Reading.WordXml;

namespace Heliograph.Pdf.Checking;

/// <summary>
/// Characters in a template none of the built-in fonts can draw, such as text in a script they do not cover or a symbol
/// font's private characters, which would print as empty boxes. Reported once in each part, at the first one. A character
/// in a recipient's data is found when the letter is made, and the conversion fails saying so.
/// </summary>
internal static class GlyphLimits
{
    private static readonly RunStyle Any = new(false, false, false, 12, "Liberation Sans");

    public static IEnumerable<Diagnostic> Check(DocumentToCheck document)
    {
        foreach (var part in document.Parts)
        {
            var text = part.Xml.Descendants(W + "t").FirstOrDefault(t => t.Value.EnumerateRunes().Any(r => FontLibrary.Covering(Any, r.Value) is null));
            if (text is not null)
            {
                yield return new Diagnostic(DiagnosticCodes.PdfCannotDrawCharacter,
                    $"This document has a character that none of the fonts {LayoutLimits.Route} has can draw, so it would print as an empty box.",
                    $"Use a character from the Latin, Greek or Cyrillic alphabets, or ordinary punctuation, {LayoutLimits.Instead}", part.Locate(text));
            }
        }
    }
}
