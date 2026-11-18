using System.Xml.Linq;
using Heliograph.Pdf.Reading;
using static Heliograph.Pdf.Reading.WordXml;

namespace Heliograph.Pdf.Checking;

/// <summary>
/// A section's page settings the converter lays out: its size, orientation and margins, its headers and footers, a first
/// page of its own, and page numbers from where it says in figures, letters or Roman numerals. A section that starts
/// other than on a new page, and anything else on the page, is not yet.
/// </summary>
internal static class SectionLimits
{
    public static IEnumerable<(XElement, string)> Found(XElement root) =>
        root.Descendants(W + "sectPr").SelectMany(s => s.Elements().Select(e => (e, What: What(e))).Where(f => f.What is not null))!;

    private static string? What(XElement e) => e.Name.LocalName switch
    {
        "type" when Val(e) is not (null or "nextPage") => Val(e) == "continuous" ? "a section break that does not start a new page" : "a section break that starts on an odd or even page",
        "pgBorders" => "a border round the page",
        "lnNumType" => "line numbers in the margin",
        "vAlign" when Val(e) is not (null or "top") => "text set in the middle or spread down the page",
        "pgNumType" when (string?)e.Attribute(W + "fmt") is { } fmt && !Numbered(fmt) => "page numbers in a format of their own",
        "pgNumType" when e.Attribute(W + "chapStyle") is not null => "page numbers with chapter numbers",
        "docGrid" when (string?)e.Attribute(W + "type") is not (null or "default") => "a document grid that lines text up",
        _ => null,
    };

    private static bool Numbered(string format) => format is "decimal" or "lowerRoman" or "upperRoman" or "lowerLetter" or "upperLetter";
}
