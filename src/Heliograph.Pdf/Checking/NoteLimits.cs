using System.Xml.Linq;
using Heliograph.Documents;
using static Heliograph.Pdf.Reading.WordXml;

namespace Heliograph.Pdf.Checking;

/// <summary>
/// Notes the converter lays out: footnotes at the foot of each page, referred to from the body outside a table, and
/// endnotes after the letter, numbered through the letter as the document's settings say, holding paragraphs and pictures
/// in line with the text. Anything else is not yet.
/// </summary>
internal static class NoteLimits
{
    private static readonly XNamespace Wp = "http://schemas.openxmlformats.org/wordprocessingml/2006/wordprocessingDrawing";

    /// <summary>The document's own note settings, which apply wherever a section does not set its own.</summary>
    public static IEnumerable<Diagnostic> Check(DocumentToCheck document) => NoteSettings.Check(document);

    public static IEnumerable<(XElement, string)> Found(XElement root) =>
        root.Descendants().Select(e => (e, What: What(e))).Where(f => f.What is not null)!;

    private static string? What(XElement e) => e.Name.LocalName switch
    {
        _ when e.Name.Namespace != W && e.Name != Wp + "anchor" => null,
        "footnoteReference" when e.Ancestors(W + "tbl").Any() => "a footnote in a table",
        "footnoteReference" or "endnoteReference" when On(e.Attribute(W + "customMarkFollows")) => "a note with a mark of its own",
        "footnotePr" or "endnotePr" when e.Parent?.Name == W + "sectPr" => NoteSettings.InSection(e),
        "tbl" when InNote(e) => "a table in a note",
        "anchor" when InNote(e) => "a picture that floats in a note",
        _ => null,
    };

    private static bool InNote(XElement e) => e.Ancestors().Any(a => a.Name == W + "footnote" || a.Name == W + "endnote");

    private static bool On(XAttribute? a) => a is not null && (string)a is "1" or "true" or "on";
}
