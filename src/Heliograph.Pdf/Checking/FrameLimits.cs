using System.Xml.Linq;
using static Heliograph.Pdf.Reading.WordXml;

namespace Heliograph.Pdf.Checking;

/// <summary>
/// Borders and shading round a paragraph: drawn in the body, not yet in a table cell, header or footer, nor a bar beside a
/// paragraph or shading behind a whole table.
/// </summary>
internal static class FrameLimits
{
    public static string? Border(XElement pBdr)
    {
        if (Elsewhere(pBdr))
        {
            return "a border round a paragraph in a table, header or footer";
        }

        return pBdr.Element(W + "bar") is not null ? "a bar beside a paragraph" : null;
    }

    public static string? Shading(XElement shd)
    {
        var drawn = shd.Parent?.Name == W + "pPr" && !Elsewhere(shd);
        var colour = (string?)shd.Attribute(W + "fill");
        return drawn || colour is null or "auto" or "FFFFFF" or "ffffff" ? null : "shading behind a paragraph in a table, header or footer, or behind a whole table";
    }

    // Whether an element is in a table cell, a header or a footer, where the converter does not draw a paragraph's frame.
    private static bool Elsewhere(XElement e) => e.Ancestors(W + "tc").Any() || e.Ancestors().Any(a => a.Name.LocalName is "hdr" or "ftr");
}
