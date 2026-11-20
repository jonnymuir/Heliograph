using System.Xml.Linq;
using static Heliograph.Pdf.Reading.WordXml;

namespace Heliograph.Pdf.Checking;

/// <summary>
/// What a run may hold beyond text, tabs, breaks, pictures, notes and fields that the converter does not lay out yet, and
/// would otherwise leave out of the PDF or draw wrongly: a symbol from a symbol font, an optional hyphen, a phonetic guide, a
/// date block, an equation, a border round text, and an underline dotted, dashed or wavy.
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

    private static readonly XNamespace Math = "http://schemas.openxmlformats.org/officeDocument/2006/math";

    public static IEnumerable<(XElement, string)> Found(XElement root) =>
        root.Descendants().Select(e => (e, What: What(e))).Where(f => f.What is not null)!;

    private static string? What(XElement e) => e.Name.Namespace == Math
        ? e.Name.LocalName is "oMath" or "oMathPara" && !e.Ancestors(Math + "oMathPara").Any() ? "an equation" : null
        : e.Name.Namespace != W ? null : e.Name.LocalName switch
        {
            _ when Unbuilt.TryGetValue(e.Name.LocalName, out var what) => what,
            "bdr" when e.Parent?.Name == W + "rPr" && Val(e) is not (null or "none" or "nil") => "a border round text",
            "u" when Val(e) is { } u && u is not ("none" or "single" or "words" or "double" or "thick") => "a dotted, dashed or wavy underline",
            _ => null,
        };
}
