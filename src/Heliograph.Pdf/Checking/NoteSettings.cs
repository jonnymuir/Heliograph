using System.Xml.Linq;
using Heliograph.Documents;
using static Heliograph.Pdf.Reading.WordXml;

namespace Heliograph.Pdf.Checking;

/// <summary>
/// How a document numbers and places its notes, in its settings and its sections': through the letter, in figures,
/// letters or Roman numerals from any number, footnotes at the foot of the page and endnotes at the end, every section
/// alike. Anything else is not laid out yet.
/// </summary>
internal static class NoteSettings
{
    private static readonly (string Name, string Place)[] Kinds = [("footnotePr", "pageBottom"), ("endnotePr", "docEnd")];

    public static IEnumerable<Diagnostic> Check(DocumentToCheck document)
    {
        var settings = document.Settings?.Root;
        foreach (var (name, place) in Kinds)
        {
            if (settings?.Element(W + name) is { } pr && Numbered(pr, place) is { } how)
            {
                yield return new Diagnostic(DiagnosticCodes.PdfCannotLayOutNotes, $"This document has {how}. {LayoutLimits.Route} numbers notes through the letter, footnotes at the foot of the page and endnotes after the letter.",
                    $"Set notes to number continuously, footnotes at the bottom of the page and endnotes at the end of the document (Footnote and Endnote in Word), {LayoutLimits.Instead}");
            }
        }
    }

    /// <summary>What is wrong with a section's own note settings, or null.</summary>
    public static string? InSection(XElement pr) =>
        Numbered(pr, pr.Name.LocalName == "footnotePr" ? "pageBottom" : "docEnd") ?? (Alike(pr) ? null : "notes numbered differently in one section from another");

    // Notes numbered other than through the letter in a format the converter writes, or placed somewhere else.
    private static string? Numbered(XElement pr, string place)
    {
        string? Setting(string name) => Val(pr.Element(W + name));
        return Setting("numFmt") is not (null or "decimal" or "lowerRoman" or "upperRoman" or "lowerLetter" or "upperLetter")
            || Setting("numRestart") is not (null or "continuous") || (Setting("pos") is { } pos && pos != place)
            || (Setting("numStart") is { } start && !int.TryParse(start, out _))
            ? "notes numbered or placed in a way of their own"
            : null;
    }

    // Whether every section numbers its notes of this kind as this one does: notes are numbered through the letter.
    private static bool Alike(XElement pr)
    {
        static string Key(XElement? p) => string.Concat((p?.Elements().Where(s => s.Name.LocalName is "numFmt" or "numStart") ?? []).Select(s => s.ToString(SaveOptions.DisableFormatting)));
        return (pr.Document?.Descendants(W + "sectPr") ?? []).All(s => Key(s.Element(pr.Name)) == Key(pr));
    }
}
