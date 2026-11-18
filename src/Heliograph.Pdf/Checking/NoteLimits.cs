using System.Xml.Linq;
using Heliograph.Documents;
using static Heliograph.Pdf.Reading.WordXml;

namespace Heliograph.Pdf.Checking;

/// <summary>
/// Footnotes the converter lays out: referred to from the body, outside a table, numbered 1, 2, 3 through the letter at the
/// foot of each page, holding paragraphs and pictures in line with the text. Endnotes, and anything else, are not yet.
/// </summary>
internal static class NoteLimits
{
    private static readonly XNamespace Wp = "http://schemas.openxmlformats.org/wordprocessingml/2006/wordprocessingDrawing";

    /// <summary>The document's own footnote settings, which apply wherever a section does not set its own.</summary>
    public static IEnumerable<Diagnostic> Check(DocumentToCheck document)
    {
        if (document.Settings?.Root?.Element(W + "footnotePr") is { } settings && Numbered(settings) is { } how)
        {
            yield return new Diagnostic(DiagnosticCodes.PdfCannotLayOutNotes, $"This document has {how}. {LayoutLimits.Route} numbers footnotes 1, 2, 3 at the foot of the page only.",
                $"Set footnotes to 1, 2, 3, continuous, at the bottom of the page (Footnote and Endnote in Word), {LayoutLimits.Instead}");
        }
    }

    public static IEnumerable<(XElement, string)> Found(XElement root) =>
        root.Descendants().Select(e => (e, What: What(e))).Where(f => f.What is not null)!;

    private static string? What(XElement e) => e.Name.LocalName switch
    {
        _ when e.Name.Namespace != W && e.Name != Wp + "anchor" => null,
        "endnoteReference" => "an endnote",
        "footnoteReference" when e.Ancestors(W + "tbl").Any() => "a footnote in a table",
        "footnoteReference" when On(e.Attribute(W + "customMarkFollows")) => "a footnote with a mark of its own",
        "footnotePr" when Numbered(e) is { } how => how,
        "tbl" when InNote(e) => "a table in a footnote",
        "anchor" when InNote(e) => "a picture that floats in a footnote",
        _ => null,
    };

    // Footnotes numbered other than 1, 2, 3 through the letter, or placed other than at the foot of the page.
    private static string? Numbered(XElement footnotePr)
    {
        string? Setting(string name) => Val(footnotePr.Element(W + name));
        return Setting("numFmt") is not (null or "decimal") || Setting("numStart") is not (null or "1")
            || Setting("numRestart") is not (null or "continuous") || Setting("pos") is not (null or "pageBottom")
            ? "footnotes numbered or placed in a way of their own"
            : null;
    }

    private static bool InNote(XElement e) => e.Ancestors(W + "footnote").Any();

    private static bool On(XAttribute? a) => a is not null && (string)a is "1" or "true" or "on";
}
