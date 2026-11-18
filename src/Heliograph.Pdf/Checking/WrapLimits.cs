using System.Xml.Linq;
using Heliograph.Pdf.Layout;
using Heliograph.Pdf.Reading;
using static Heliograph.Pdf.Reading.WordXml;

namespace Heliograph.Pdf.Checking;

/// <summary>
/// Text wrapped round a floating picture, as the converter lays it out: square, or above and below it, in the body of the
/// letter, text on one side of a square-wrapped picture only. Wrapped tight to its outline, in a header or footer, or with
/// room for text on both sides of it, is not yet. A picture's place across the page is worked out from its section's
/// page as the converter will place it.
/// </summary>
internal static class WrapLimits
{
    private static readonly XNamespace Wp = "http://schemas.openxmlformats.org/drawingml/2006/wordprocessingDrawing";

    public static string? Wrapped(XElement anchor)
    {
        if (anchor.Element(Wp + "wrapNone") is not null)
        {
            return null;
        }

        if (anchor.Element(Wp + "wrapSquare") is null && anchor.Element(Wp + "wrapTopAndBottom") is null)
        {
            return "a picture with text wrapped tight to its outline";
        }

        if (anchor.Ancestors().Any(a => a.Name.LocalName is "hdr" or "ftr"))
        {
            return "a picture in a header or footer with text wrapped round it";
        }

        return anchor.Element(Wp + "wrapSquare") is { } square && (string?)square.Attribute("wrapText") is null or "bothSides" && BothSides(anchor)
            ? "a picture with room for text on both sides of it"
            : null;
    }

    // Whether a picture leaves room for text on both its left and its right across its section's text area.
    private static bool BothSides(XElement anchor)
    {
        var g = PageGeometry.Of(Section(anchor));
        var h = anchor.Element(Wp + "positionH");
        var (frame, span) = FloatPlacer.Across((string?)h?.Attribute("relativeFrom") ?? "column", g);
        var width = Emu((string?)anchor.Element(Wp + "extent")?.Attribute("cx"));
        var x = FloatPlacer.Aligned(h?.Element(Wp + "align")?.Value, frame, span, width, Emu(h?.Element(Wp + "posOffset")?.Value), "left", "right");
        var (left, right) = (x - Emu((string?)anchor.Attribute("distL")) - g.Left, g.Left + g.ContentWidth - (x + width + Emu((string?)anchor.Attribute("distR"))));
        return left > 0.5 && right > 0.5;
    }

    // The section a picture is in: the settings of the next paragraph to end a section, or the body's own.
    private static XElement Section(XElement anchor)
    {
        var paragraph = anchor.Ancestors(W + "p").LastOrDefault();
        var next = paragraph?.ElementsAfterSelf().Prepend(paragraph).Select(p => p.Element(W + "pPr")?.Element(W + "sectPr")).FirstOrDefault(s => s is not null);
        return next ?? anchor.Document?.Root?.Element(W + "body")?.Element(W + "sectPr") ?? new XElement(W + "sectPr");
    }

    private static double Emu(string? value) => double.TryParse(value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var v) ? v / 12700 : 0;
}
