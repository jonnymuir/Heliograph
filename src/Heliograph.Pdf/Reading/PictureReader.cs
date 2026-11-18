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

    /// <summary>The picture in a drawing, or null for anything else (the compile-time check has refused what it cannot draw).</summary>
    public static PictureInline? Read(XElement drawing, Func<string, byte[]?> linked, RunStyle style)
    {
        var inline = drawing.Element(Wp + "inline");
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
