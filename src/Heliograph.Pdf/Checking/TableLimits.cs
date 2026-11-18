using System.Globalization;
using System.Xml.Linq;
using Heliograph.Documents;
using Heliograph.Pdf.Reading;
using static Heliograph.Pdf.Reading.WordXml;

namespace Heliograph.Pdf.Checking;

/// <summary>
/// Tables the converter cannot lay out yet beyond what LayoutLimits finds in the text: one whose style formats some of its
/// rows or columns differently (a shaded heading row or banded rows, say) where the table turns that on, and one that
/// floats with text around it. Reported once in each part, at the first such table.
/// </summary>
internal static class TableLimits
{
    // Which part of a table a conditional format applies to, and the tblLook setting that turns it on.
    private static readonly Dictionary<string, (string Attribute, int Bit, bool Inverted)> Switches = new(StringComparer.Ordinal)
    {
        ["firstRow"] = ("firstRow", 0x0020, false), ["lastRow"] = ("lastRow", 0x0040, false),
        ["firstCol"] = ("firstColumn", 0x0080, false), ["lastCol"] = ("lastColumn", 0x0100, false),
        ["band1Horz"] = ("noHBand", 0x0200, true), ["band2Horz"] = ("noHBand", 0x0200, true),
        ["band1Vert"] = ("noVBand", 0x0400, true), ["band2Vert"] = ("noVBand", 0x0400, true),
    };

    public static IEnumerable<Diagnostic> Check(DocumentToCheck document)
    {
        var styles = new StyleSheet(document.Styles);
        foreach (var part in document.Parts)
        {
            var found = part.Xml.Descendants(W + "tbl").Select(t => (Table: t, Problem: Problem(t, styles))).FirstOrDefault(t => t.Problem is not null);
            if (found.Problem is { } what)
            {
                yield return new Diagnostic(DiagnosticCodes.PdfCannotLayOutTable, $"This document has a table {what}. {LayoutLimits.Route} does not lay those out yet.",
                    $"Take that formatting off the table (Table Design in Word), or set the table in line with the text, {LayoutLimits.Instead}", part.Locate(found.Table));
            }
        }
    }

    private static string? Problem(XElement tbl, StyleSheet styles)
    {
        var tblPr = tbl.Element(W + "tblPr");
        if (tblPr?.Element(W + "tblpPr") is not null)
        {
            return "that floats, with text around it";
        }

        var look = tblPr?.Element(W + "tblLook");
        var chain = styles.Chain(Val(tblPr?.Element(W + "tblStyle")) ?? styles.DefaultTable ?? "");
        return chain.SelectMany(s => s.Elements(W + "tblStylePr")).Any(c => Applies((string?)c.Attribute(W + "type") ?? "", look) && c.HasElements)
            ? "whose style formats some rows or columns differently"
            : null;
    }

    // A conditional format applies when the table's look turns it on; one for the whole table, or a corner, always counts.
    private static bool Applies(string type, XElement? look)
    {
        if (!Switches.TryGetValue(type, out var s))
        {
            return true;
        }

        var on = (string?)look?.Attribute(W + s.Attribute) is { } flag
            ? flag is "1" or "true" or "on"
            : int.TryParse(Val(look), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var bits) && (bits & s.Bit) != 0;
        return on != s.Inverted;
    }
}
