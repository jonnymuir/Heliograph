using System.Xml.Linq;
using Heliograph.Pdf.Reading;
using static Heliograph.Pdf.Reading.WordXml;

namespace Heliograph.Pdf.Checking;

/// <summary>
/// A section's page settings the converter lays out: its size, orientation and margins, its headers and footers, a first
/// page of its own, and page numbers from where it says in figures, letters or Roman numerals, and page breaks in the
/// body. A section that starts other than on a new page, a page break elsewhere, and anything else on the page, is not yet.
/// </summary>
internal static class SectionLimits
{
    public static IEnumerable<(XElement, string)> Found(XElement root) =>
        root.Descendants(W + "sectPr").SelectMany(s => s.Elements().Select(e => (e, What: What(e))).Where(f => f.What is not null))
            .Concat(Breaks(root))!;

    // A page break, or a column break, starts a new page in the body; in a table, a header, a footer or a note it is not laid out yet.
    private static IEnumerable<(XElement, string?)> Breaks(XElement root) =>
        root.Descendants(W + "br").Where(b => (string?)b.Attribute(W + "type") is "page" or "column")
            .Where(b => b.Ancestors(W + "tbl").Any() || b.Ancestors().Any(a => a.Name.LocalName is "hdr" or "ftr" or "footnotes" or "endnotes"))
            .Select(b => (b, (string?)"a page break in a table, header, footer or note"));

    private static string? What(XElement e) => e.Name.LocalName switch
    {
        "type" when Val(e) is not (null or "nextPage") => Val(e) == "continuous" ? "a section break that does not start a new page" : "a section break that starts on an odd or even page",
        "pgBorders" => "a border round the page",
        "pgMar" when (double?)e.Attribute(W + "gutter") > 0 => "a gutter beside the margin",
        "lnNumType" => "line numbers in the margin",
        "vAlign" when Val(e) is not (null or "top") => "text set in the middle or spread down the page",
        "pgNumType" when (string?)e.Attribute(W + "fmt") is { } fmt && !Numbered(fmt) => "page numbers in a format of their own",
        "pgNumType" when e.Attribute(W + "chapStyle") is not null => "page numbers with chapter numbers",
        "docGrid" when (string?)e.Attribute(W + "type") is not (null or "default") => "a document grid that lines text up",
        _ => null,
    };

    private static bool Numbered(string format) => format is "decimal" or "lowerRoman" or "upperRoman" or "lowerLetter" or "upperLetter";
}
