using System.Globalization;
using System.Xml.Linq;
using static Heliograph.Pdf.Reading.WordXml;

namespace Heliograph.Pdf.Reading;

/// <summary>A paragraph's borders and shading through its layers of properties, each side from the last that sets it.</summary>
internal static class FrameReader
{
    public static ParagraphFrame? Read(IReadOnlyList<XElement> layers)
    {
        BorderLine? Side(string name) => Line(layers.Select(l => l.Element(W + "pBdr")?.Element(W + name)).LastOrDefault(e => e is not null));
        var shd = layers.Select(l => l.Element(W + "shd")).LastOrDefault(e => e is not null);
        var frame = new ParagraphFrame(Side("top"), Side("bottom"), Side("left") ?? Side("start"), Side("right") ?? Side("end"), Side("between"), Shading(shd));
        return frame is { Top: null, Bottom: null, Left: null, Right: null, Between: null, Shading: null } ? null : frame;
    }

    // A border's size is in eighths of a point and its space in points.
    private static BorderLine? Line(XElement? e) =>
        e is null || Val(e) is "nil" or "none"
            ? null
            : new BorderLine(Number((string?)e.Attribute(W + "sz") ?? "4") / 8, Number((string?)e.Attribute(W + "space") ?? "0"));

    // Clear shading is its fill; solid shading is its pattern colour, black when automatic.
    private static string? Shading(XElement? shd)
    {
        var colour = Val(shd) == "solid" ? (string?)shd!.Attribute(W + "color") ?? "000000" : (string?)shd?.Attribute(W + "fill");
        return colour is { Length: 6 } && colour is not ("auto" or "FFFFFF" or "ffffff")
            && int.TryParse(colour, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out _) ? colour.ToUpperInvariant() : null;
    }
}
