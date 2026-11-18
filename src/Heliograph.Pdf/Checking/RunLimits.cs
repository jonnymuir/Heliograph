using System.Xml.Linq;
using static Heliograph.Pdf.Reading.WordXml;

namespace Heliograph.Pdf.Checking;

/// <summary>
/// What a run may hold beyond text, tabs, breaks, pictures, notes and fields that the converter does not lay out yet, and
/// would otherwise leave out of the PDF: a symbol from a symbol font, an optional hyphen, a phonetic guide, a date block.
/// </summary>
internal static class RunLimits
{
    private static readonly Dictionary<string, string> Unbuilt = new(StringComparer.Ordinal)
    {
        ["sym"] = "a symbol inserted from a symbol font, such as Wingdings",
        ["softHyphen"] = "an optional hyphen",
        ["ruby"] = "a phonetic guide over text",
        ["dayShort"] = "a date block",
        ["dayLong"] = "a date block",
        ["monthShort"] = "a date block",
        ["monthLong"] = "a date block",
        ["yearShort"] = "a date block",
        ["yearLong"] = "a date block",
    };

    public static IEnumerable<(XElement, string)> Found(XElement root) =>
        root.Descendants().Where(e => e.Name.Namespace == W && Unbuilt.ContainsKey(e.Name.LocalName)).Select(e => (e, Unbuilt[e.Name.LocalName]));
}
