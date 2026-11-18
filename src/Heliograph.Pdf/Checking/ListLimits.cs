using System.Globalization;
using System.Xml.Linq;
using Heliograph.Documents;
using Heliograph.Pdf.Fonts;
using Heliograph.Pdf.Reading;
using static Heliograph.Pdf.Reading.WordXml;

namespace Heliograph.Pdf.Checking;

/// <summary>
/// List labels the converter cannot draw yet: a number style it does not write, a picture as a bullet, or a bullet it
/// has no shape for. Each level a list item uses is checked, and each level its label shows ("%1.%2."), once in each
/// part, at the first item.
/// </summary>
internal static class ListLimits
{
    public static IEnumerable<Diagnostic> Check(DocumentToCheck document)
    {
        if (document.Numbering is null)
        {
            yield break;
        }

        var numbering = new NumberingDefinitions(document.Numbering);
        var styles = new StyleSheet(document.Styles);
        foreach (var part in document.Parts)
        {
            foreach (var p in part.Xml.Descendants(W + "p"))
            {
                if (numbering.Place(NumPr(p, styles)) is { } place && Problem(numbering, place) is { } what)
                {
                    yield return new Diagnostic(DiagnosticCodes.PdfCannotDrawListLabel,
                        $"This document has {what}. {LayoutLimits.Route} cannot draw that list's labels yet.",
                        $"Use a numbered, lettered or Roman-numeral list, or an ordinary bullet, {LayoutLimits.Instead}", part.Locate(p));
                    break;
                }
            }
        }
    }

    // A paragraph's own numbering, or its style's.
    private static XElement? NumPr(XElement p, StyleSheet styles)
    {
        var pPr = p.Element(W + "pPr");
        return pPr?.Element(W + "numPr")
            ?? styles.Chain(Val(pPr?.Element(W + "pStyle")) ?? styles.DefaultParagraph).Select(s => s.Element(W + "pPr")?.Element(W + "numPr")).LastOrDefault(n => n is not null);
    }

    private static string? Problem(NumberingDefinitions numbering, ListPlace place)
    {
        var text = Val(place.Definition.Element(W + "lvlText")) ?? "";
        var shown = Enumerable.Range(0, 9).Where(l => l == place.Level || text.Contains($"%{l + 1}", StringComparison.Ordinal));
        foreach (var level in shown)
        {
            if (numbering.Definition(place.NumId, place.AbstractId, level) is { } definition && Problem(definition) is { } what)
            {
                return what;
            }
        }

        return null;
    }

    private static string? Problem(XElement level)
    {
        var format = Val(level.Element(W + "numFmt")) ?? "decimal";
        if (!ListLabels.Formats.Contains(format))
        {
            return string.Create(CultureInfo.InvariantCulture, $"a list numbered in the style \"{format}\"");
        }

        if (level.Element(W + "lvlPicBulletId") is not null)
        {
            return "a picture used as a bullet";
        }

        if (format != "bullet")
        {
            return null;
        }

        var font = (string?)level.Element(W + "rPr")?.Element(W + "rFonts")?.Attribute(W + "ascii") ?? "";
        var (bullet, ownFont) = ListLabels.Bullet(Val(level.Element(W + "lvlText")) ?? "", font);
        var face = FontLibrary.Face(new RunStyle(false, false, false, 12, ownFont ? font : ""));
        return bullet.EnumerateRunes().All(r => face.Has(r.Value)) ? null : "a bullet character it has no shape for";
    }
}
