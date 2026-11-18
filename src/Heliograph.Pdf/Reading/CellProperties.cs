using System.Globalization;
using System.Xml.Linq;
using static Heliograph.Pdf.Reading.WordXml;

namespace Heliograph.Pdf.Reading;

/// <summary>A cell's own settings: where its text sits, how it is merged down its column, and its shading.</summary>
internal static class CellProperties
{
    public static Cell Read(Cell cell, XElement? tcPr)
    {
        var merge = tcPr?.Element(W + "vMerge");
        return cell with
        {
            VAlign = Val(tcPr?.Element(W + "vAlign")) switch { "center" => "center", "bottom" => "bottom", _ => "top" },
            VMerge = merge is null ? "" : Val(merge) == "restart" ? "restart" : "continue",
            Background = Shading(tcPr?.Element(W + "shd")),
        };
    }

    // Clear shading is its fill; solid shading is its pattern colour, black when automatic.
    private static string? Shading(XElement? shd)
    {
        var colour = Val(shd) == "solid" ? (string?)shd!.Attribute(W + "color") ?? "000000" : (string?)shd?.Attribute(W + "fill");
        return colour is { Length: 6 } && colour != "auto" && int.TryParse(colour, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out _)
            ? colour.ToUpperInvariant()
            : null;
    }
}
