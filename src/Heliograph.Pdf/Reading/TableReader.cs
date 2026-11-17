using System.Xml.Linq;
using static Heliograph.Pdf.Reading.WordXml;

namespace Heliograph.Pdf.Reading;

/// <summary>Reads a table: its column grid, cell padding, indent, and each row's cells with their borders.</summary>
internal static class TableReader
{
    public static Table Read(XElement tbl, Func<XElement, IReadOnlyList<Block>> blocks)
    {
        var tblPr = tbl.Element(W + "tblPr");
        var grid = tbl.Element(W + "tblGrid")?.Elements(W + "gridCol").Select(g => Twips(g, "w") ?? 0).ToList() ?? [];
        var tableBorders = tblPr?.Element(W + "tblBorders");
        var rows = tbl.Elements(W + "tr").Select(tr => new Row(
            [.. tr.Elements(W + "tc").Select(tc => Cell(tc, tableBorders, blocks))],
            On(tr.Element(W + "trPr")?.Element(W + "tblHeader")),
            On(tr.Element(W + "trPr")?.Element(W + "cantSplit")))).ToList();

        return new Table(grid, rows, Twips(tblPr?.Element(W + "tblInd"), "w") ?? 0, Margins(tblPr?.Element(W + "tblCellMar")));
    }

    // Word's default cell padding is 0.08 inch either side and none above or below.
    private static CellMargins Margins(XElement? margins) => new(
        Twips(margins?.Element(W + "left"), "w") ?? 5.4,
        Twips(margins?.Element(W + "right"), "w") ?? 5.4,
        Twips(margins?.Element(W + "top"), "w") ?? 0,
        Twips(margins?.Element(W + "bottom"), "w") ?? 0);

    private static Cell Cell(XElement tc, XElement? tableBorders, Func<XElement, IReadOnlyList<Block>> blocks)
    {
        var tcPr = tc.Element(W + "tcPr");
        var borders = tcPr?.Element(W + "tcBorders");
        Border? Side(string side) => BorderOf(borders?.Element(W + side) ?? tableBorders?.Element(W + side));
        var span = (int)Number(Val(tcPr?.Element(W + "gridSpan")) ?? "1");
        return new Cell(blocks(tc), span, new CellBorders(Side("top"), Side("left"), Side("bottom"), Side("right")));
    }

    // A border's size is in eighths of a point.
    private static Border? BorderOf(XElement? e) =>
        e is null || Val(e) is "nil" or "none" ? null : new Border(Number((string?)e.Attribute(W + "sz") ?? "4") / 8);
}
