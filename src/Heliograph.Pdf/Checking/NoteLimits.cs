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
    public static IEnumerable<Diagnostic> Check(DocumentToCheck document)
    {
        var settings = document.Settings?.Root;
        foreach (var (name, place) in new[] { ("footnotePr", "pageBottom"), ("endnotePr", "docEnd") })
        {
            if (settings?.Element(W + name) is { } pr && Numbered(pr, place) is { } how)
            {
                yield return new Diagnostic(DiagnosticCodes.PdfCannotLayOutNotes, $"This document has {how}. {LayoutLimits.Route} numbers notes through the letter, footnotes at the foot of the page and endnotes after the letter.",
                    $"Set notes to number continuously, footnotes at the bottom of the page and endnotes at the end of the document (Footnote and Endnote in Word), {LayoutLimits.Instead}");
            }
        }
    }

    public static IEnumerable<(XElement, string)> Found(XElement root) =>
        root.Descendants().Select(e => (e, What: What(e))).Where(f => f.What is not null)!;

    private static string? What(XElement e) => e.Name.LocalName switch
    {
        _ when e.Name.Namespace != W && e.Name != Wp + "anchor" => null,
        "footnoteReference" when e.Ancestors(W + "tbl").Any() => "a footnote in a table",
        "footnoteReference" or "endnoteReference" when On(e.Attribute(W + "customMarkFollows")) => "a note with a mark of its own",
        "footnotePr" or "endnotePr" when e.Parent?.Name == W + "sectPr" && Numbered(e, e.Name.LocalName == "footnotePr" ? "pageBottom" : "docEnd") is { } how => how,
        "footnotePr" or "endnotePr" when e.Parent?.Name == W + "sectPr" && !Alike(e) => "notes numbered differently in one section from another",
        "tbl" when InNote(e) => "a table in a note",
        "anchor" when InNote(e) => "a picture that floats in a note",
        _ => null,
    };

    // Notes numbered other than through the letter in a format the converter writes, or placed somewhere else.
    private static string? Numbered(XElement pr, string place)
    {
        string? Setting(string name) => Val(pr.Element(W + name));
        return Setting("numFmt") is not (null or "decimal" or "lowerRoman" or "upperRoman" or "lowerLetter" or "upperLetter")
            || Setting("numRestart") is not (null or "continuous") || Setting("pos") is not null && Setting("pos") != place
            || Setting("numStart") is { } start && !int.TryParse(start, out _)
            ? "notes numbered or placed in a way of their own"
            : null;
    }

    // Whether every section numbers its notes of this kind as this one does: notes are numbered through the letter.
    private static bool Alike(XElement pr)
    {
        static string Key(XElement? p) => string.Concat((p?.Elements().Where(s => s.Name.LocalName is "numFmt" or "numStart") ?? []).Select(s => s.ToString(SaveOptions.DisableFormatting)));
        var sections = pr.Document?.Descendants(W + "sectPr") ?? [];
        return sections.All(s => Key(s.Element(pr.Name)) == Key(pr));
    }

    private static bool InNote(XElement e) => e.Ancestors().Any(a => a.Name == W + "footnote" || a.Name == W + "endnote");

    private static bool On(XAttribute? a) => a is not null && (string)a is "1" or "true" or "on";
}
