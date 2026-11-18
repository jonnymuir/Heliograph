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
            "pBdr" => "a border round a paragraph",
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

        return (string?)shd.Attribute(W + "fill") is { } fill && fill is not ("auto" or "FFFFFF" or "ffffff") ? "shading behind a paragraph or cell" : null;
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
