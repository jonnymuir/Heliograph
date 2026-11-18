using System.Xml.Linq;
using static Heliograph.Pdf.Reading.WordXml;

namespace Heliograph.Pdf.Checking;

/// <summary>
/// Formatting the converter cannot draw yet. Colour, highlight, run shading, strikethrough, superscript, subscript,
/// capitals and character spacing are drawn; these are not, and would print as plain text.
/// </summary>
internal static class FormattingLimits
{
    // Formatting that is either on or off.
    private static readonly Dictionary<string, string> Switches = new(StringComparer.Ordinal)
    {
        ["smallCaps"] = "small capitals",
        ["emboss"] = "embossed, engraved, outlined or shadowed text",
        ["imprint"] = "embossed, engraved, outlined or shadowed text",
        ["outline"] = "embossed, engraved, outlined or shadowed text",
        ["shadow"] = "embossed, engraved, outlined or shadowed text",
    };

    public static IEnumerable<(XElement, string)> Found(XElement root) =>
        root.Descendants().Where(e => e.Name.Namespace == W).Select(e => (e, What: Name(e))).Where(f => f.What is not null)!;

    private static string? Name(XElement e) =>
        Switches.TryGetValue(e.Name.LocalName, out var name) ? (On(e) ? name : null) : e.Name.LocalName switch
        {
            "shd" => Shading(e),
            "w" or "position" => Scaled(e),
            "em" when Val(e) is not (null or "none") => "emphasis marks",
            "fitText" => "text fitted to a set width",
            "pBdr" when e.Ancestors(W + "tc").Any() || e.Ancestors().Any(a => a.Name.LocalName is "hdr" or "ftr") => "a border round a paragraph in a table, header or footer",
            "pBdr" when e.Element(W + "bar") is not null => "a bar beside a paragraph",
            "spacing" when Auto(e, "beforeAutospacing") || Auto(e, "afterAutospacing") => "spacing set to automatic, as in text pasted from a web page",
            _ => null,
        };

    private static bool Auto(XElement spacing, string attribute) => (string?)spacing.Attribute(W + attribute) is "1" or "true" or "on";

    // Clear or solid shading behind a run is drawn; patterned shading, or shading behind a paragraph or cell, is not.
    private static string? Shading(XElement shd)
    {
        if (shd.Parent?.Name == W + "rPr")
        {
            return Val(shd) is null or "clear" or "solid" or "nil" ? null : "patterned shading";
        }

        // A cell's shading is drawn; a paragraph's, or a whole table's, is not yet.
        if (shd.Parent?.Name == W + "tcPr")
        {
            return Val(shd) is null or "clear" or "solid" or "nil" ? null : "patterned shading";
        }

        // A paragraph's shading is drawn in the body; in a table, header or footer, or behind a whole table, it is not yet.
        var inBody = shd.Parent?.Name == W + "pPr" && !shd.Ancestors(W + "tc").Any() && !shd.Ancestors().Any(a => a.Name.LocalName is "hdr" or "ftr");
        return inBody || (string?)shd.Attribute(W + "fill") is not { } fill || fill is "auto" or "FFFFFF" or "ffffff" ? null : "shading behind a paragraph in a table, header or footer, or behind a whole table";
    }

    // Characters scaled, or raised or lowered by a set amount, in a run.
    private static string? Scaled(XElement e)
    {
        if (e.Parent?.Name != W + "rPr" || Val(e) is not { } value || value == (e.Name.LocalName == "w" ? "100" : "0"))
        {
            return null;
        }

        return e.Name.LocalName == "w" ? "stretched or squeezed characters" : "text raised or lowered by a set amount";
    }
}
