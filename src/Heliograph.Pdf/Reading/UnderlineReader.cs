using System.Xml.Linq;
using static Heliograph.Pdf.Reading.WordXml;

namespace Heliograph.Pdf.Reading;

/// <summary>How a run is underlined: its style (single, under the words only, double or thick) and its own colour, if any.</summary>
internal static class UnderlineReader
{
    public static RunStyle Read(RunStyle style, XElement u)
    {
        var colour = (string?)u.Attribute(W + "color");
        return style with
        {
            Underline = Val(u) is not ("none" or null),
            UnderlineStyle = Val(u) is "words" or "double" or "thick" ? Val(u)! : "single",
            UnderlineColor = colour is { Length: 6 } && colour != "auto" ? colour.ToUpperInvariant() : null,
        };
    }
}
