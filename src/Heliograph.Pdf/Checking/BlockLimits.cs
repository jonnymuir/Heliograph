using System.Xml.Linq;
using static Heliograph.Pdf.Reading.WordXml;

namespace Heliograph.Pdf.Checking;

/// <summary>
/// Paragraph and block content the converter would otherwise set out wrongly or leave out: a paragraph in a frame (a drop
/// capital among them), content imported from another file, text spread across the whole line, and a section break inside
/// a content control or custom XML.
/// </summary>
internal static class BlockLimits
{
    public static IEnumerable<(XElement, string)> Found(XElement root) =>
        root.Descendants().Where(e => e.Name.Namespace == W).Select(e => (e, What: What(e))).Where(f => f.What is not null)!;

    private static string? What(XElement e) => e.Name.LocalName switch
    {
        "framePr" => e.Attribute(W + "dropCap") is { } drop && (string)drop != "none" ? "a drop capital" : "a paragraph in a frame",
        "altChunk" => "content imported from another file",
        "jc" when e.Parent?.Name == W + "pPr" && Val(e) is "distribute" or "thaiDistribute" or "lowKashida" or "mediumKashida" or "highKashida" or "numTab" => "text spread across the whole line",
        "sectPr" when e.Parent?.Name == W + "pPr" && e.Ancestors().Any(a => a.Name == W + "sdt" || a.Name == W + "customXml") => "a section break inside a content control",
        _ => null,
    };
}
