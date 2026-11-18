using System.Globalization;
using System.Xml.Linq;
using Heliograph.Pdf.Pictures;

namespace Heliograph.Pdf.Reading;

/// <summary>Reads a picture set in the line (wp:inline): its file, its size, and what it shows for a screen reader.</summary>
internal static class PictureReader
{
    private const double PointsPerEmu = 1 / 12700.0;
    private static readonly XNamespace Wp = "http://schemas.openxmlformats.org/drawingml/2006/wordprocessingDrawing";
    private static readonly XNamespace A = "http://schemas.openxmlformats.org/drawingml/2006/main";
    private static readonly XNamespace R = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
    private static readonly XNamespace Decorative = "http://schemas.microsoft.com/office/drawing/2017/decorative";

    /// <summary>
    /// The picture in a drawing, set in the line or floating with no text wrapped round it, or null for anything else (the
    /// compile-time check has refused what it cannot draw).
    /// </summary>
    public static Inline? Read(XElement drawing, Func<string, byte[]?> linked, RunStyle style)
    {
        if (drawing.Element(Wp + "anchor") is { } anchor)
        {
            return Picture(anchor, linked, style) is { } picture ? new FloatingPicture(picture, Position(anchor), On(anchor.Attribute("behindDoc"))) { Wrap = Wrap(anchor) } : null;
        }

        return Picture(drawing.Element(Wp + "inline"), linked, style);
    }

    private static bool On(XAttribute? a) => (string?)a is "1" or "true";

    // How text goes round the picture, and the space kept clear round it.
    private static TextWrap Wrap(XElement anchor)
    {
        var kind = anchor.Element(Wp + "wrapSquare") is not null ? WrapKind.Square : anchor.Element(Wp + "wrapTopAndBottom") is not null ? WrapKind.TopAndBottom : WrapKind.None;
        var side = (string?)anchor.Element(Wp + "wrapSquare")?.Attribute("wrapText") switch
        {
            "left" => WrapSide.Left,
            "right" => WrapSide.Right,
            "largest" => WrapSide.Largest,
            _ => WrapSide.BothSides,
        };
        return new TextWrap(kind, side, new Clearance(Emu(anchor, "distT"), Emu(anchor, "distB"), Emu(anchor, "distL"), Emu(anchor, "distR")));
    }

    private static Anchor Position(XElement anchor)
    {
        var (h, v) = (anchor.Element(Wp + "positionH"), anchor.Element(Wp + "positionV"));
        return new Anchor(
            (string?)h?.Attribute("relativeFrom") ?? "column", Emu(h?.Element(Wp + "posOffset")), h?.Element(Wp + "align")?.Value,
            (string?)v?.Attribute("relativeFrom") ?? "paragraph", Emu(v?.Element(Wp + "posOffset")), v?.Element(Wp + "align")?.Value);
    }

    private static double Emu(XElement? offset) =>
        double.TryParse(offset?.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? v * PointsPerEmu : 0;

    private static PictureInline? Picture(XElement? inline, Func<string, byte[]?> linked, RunStyle style)
    {
        var embed = (string?)inline?.Descendants(A + "blip").FirstOrDefault()?.Attribute(R + "embed");
        if (inline is null || embed is null || linked(embed) is not { } bytes)
        {
            return null;
        }

        var extent = inline.Element(Wp + "extent");
        var (width, height) = (Emu(extent, "cx"), Emu(extent, "cy"));
        return width > 0 && height > 0 ? new PictureInline(PictureFile.Read(bytes), width, height, Alt(inline.Element(Wp + "docPr")), style) : null;
    }

    private static double Emu(XElement? e, string attribute) =>
        double.TryParse((string?)e?.Attribute(attribute), NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? v * PointsPerEmu : 0;

    // A description, unless the picture is marked as decorative.
    private static string? Alt(XElement? docPr)
    {
        var decorative = docPr?.Descendants(Decorative + "decorative").Any(d => (string?)d.Attribute("val") is "1" or "true") == true;
        var description = (string?)docPr?.Attribute("descr");
        return decorative || string.IsNullOrWhiteSpace(description) ? null : description.Trim();
    }
}
