using System.Xml.Linq;
using Heliograph.Documents;
using Heliograph.Pdf.Fonts;
using Heliograph.Pdf.Reading;
using static Heliograph.Pdf.Reading.WordXml;

namespace Heliograph.Pdf.Checking;

/// <summary>
/// Fonts the converter has no look-alike for: named in the text or in the styles, directly or through the document's
/// theme. Each is a warning, not an error: the text is drawn in Liberation Sans, so lines may break in different places.
/// Reported once in each part, and once for the styles.
/// </summary>
internal static class FontLimits
{
    private const string Fix = "Use Arial, Times New Roman, Courier New, Calibri or Cambria, which are drawn in look-alikes with the same widths.";

    public static IEnumerable<Diagnostic> Check(DocumentToCheck document)
    {
        var theme = ThemeFonts.Read(document.Theme);
        foreach (var part in document.Parts)
        {
            if (Unknown(part.Xml, theme) is ({ } place, var font))
            {
                yield return new Diagnostic(DiagnosticCodes.PdfFontDrawnInLookAlike, Message(font), Fix, part.Locate(place), DiagnosticSeverity.Warning);
            }
        }

        if (document.Styles is not null && Unknown(UsedStyles(document.Styles, document.Parts), theme) is (not null, var styled))
        {
            yield return new Diagnostic(DiagnosticCodes.PdfFontDrawnInLookAlike, Message(styled) + " It is set in the document's styles.", Fix,
                Severity: DiagnosticSeverity.Warning);
        }
    }

    private static string Message(string font) =>
        $"This document uses the font {font}, which {LayoutLimits.Route} does not have. It is drawn in Liberation Sans, so lines may break in different places from your word processor.";

    // The document's defaults, and each style the text uses, with every style those are based on. A style no paragraph,
    // run or table uses, such as Word's own Heading 1 in Calibri Light, does not matter.
    private static IEnumerable<XElement> UsedStyles(XDocument styles, IReadOnlyList<DocumentPart> parts)
    {
        var byId = styles.Root?.Elements(W + "style").Where(s => s.Attribute(W + "styleId") is not null)
            .GroupBy(s => (string)s.Attribute(W + "styleId")!).ToDictionary(g => g.Key, g => g.First()) ?? [];
        var used = parts.SelectMany(p => p.Xml.Descendants().Where(e => e.Name == W + "pStyle" || e.Name == W + "rStyle" || e.Name == W + "tblStyle"))
            .Select(Val).OfType<string>()
            .Concat(byId.Values.Where(s => (string?)s.Attribute(W + "default") is "1" or "true" or "on").Select(s => (string)s.Attribute(W + "styleId")!));
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var queue = new Queue<string>(used);
        while (queue.TryDequeue(out var id))
        {
            if (seen.Add(id) && byId.TryGetValue(id, out var style) && Val(style.Element(W + "basedOn")) is { } basedOn)
            {
                queue.Enqueue(basedOn);
            }
        }

        return [.. styles.Root?.Elements(W + "docDefaults") ?? [], .. seen.Where(byId.ContainsKey).Select(id => byId[id])];
    }

    // The first font named that has no look-alike; a theme font with no theme to say what it is counts too.
    private static (XElement? Place, string Font) Unknown(XDocument xml, ThemeFonts theme) => Unknown([xml.Root!], theme);

    private static (XElement? Place, string Font) Unknown(IEnumerable<XElement> roots, ThemeFonts theme)
    {
        foreach (var rFonts in roots.SelectMany(r => r.Descendants(W + "rFonts")))
        {
            var font = theme.Of(rFonts) ?? (rFonts.Attribute(W + "asciiTheme") is not null ? "set by the document's theme" : null);
            if (font is not null && !FontLibrary.HasLookAlike(font))
            {
                return (rFonts, font);
            }
        }

        return (null, "");
    }
}
