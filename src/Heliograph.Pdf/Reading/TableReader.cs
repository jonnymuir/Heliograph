using System.Xml.Linq;
using static Heliograph.Pdf.Reading.WordXml;

namespace Heliograph.Pdf.Reading;

/// <summary>
/// Reads a table through its style: its column grid, cell padding, indent and alignment, each row's height and cells, and
/// each cell's borders (the table's outer borders on its edges, its inside borders between cells, a cell's own over
/// both) and where its text sits.
/// </summary>
internal static class TableReader
{
    public static Table Read(XElement tbl, TableStyle style, Func<XElement, IReadOnlyList<Block>> blocks)
    {
        var grid = tbl.Element(W + "tblGrid")?.Elements(W + "gridCol").Select(g => Twips(g, "w") ?? 0).ToList() ?? [];
        var trs = tbl.Elements(W + "tr").ToList();
        var rows = trs.Select((tr, r) => Row(tr, new Edges(r == 0, r == trs.Count - 1), style, blocks)).ToList();
        return new Table(grid, rows, Twips(style.Property("tblInd"), "w") ?? 0, Margins(style))
        {
            Align = Val(style.Property("jc")) switch { "center" => "center", "right" or "end" => "right", _ => "left" },
        };
    }

    private static Row Row(XElement tr, Edges edges, TableStyle style, Func<XElement, IReadOnlyList<Block>> blocks)
    {
        var trPr = tr.Element(W + "trPr");
        var tcs = tr.Elements(W + "tc").ToList();
        var cells = tcs.Select((tc, c) => Cell(tc, edges with { Left = c == 0, Right = c == tcs.Count - 1 }, style, blocks)).ToList();
        var height = trPr?.Element(W + "trHeight");
        return new Row(cells, On(trPr?.Element(W + "tblHeader")), On(trPr?.Element(W + "cantSplit")))
        {
            Height = Twips(height, "val") ?? 0,
            ExactHeight = (string?)height?.Attribute(W + "hRule") == "exact",
        };
    }

    // Word's default cell padding is 0.08 inch either side and none above or below.
    private static CellMargins Margins(TableStyle style) => new(
        Twips(style.Property("tblCellMar", "left") ?? style.Property("tblCellMar", "start"), "w") ?? 5.4,
        Twips(style.Property("tblCellMar", "right") ?? style.Property("tblCellMar", "end"), "w") ?? 5.4,
        Twips(style.Property("tblCellMar", "top"), "w") ?? 0,
        Twips(style.Property("tblCellMar", "bottom"), "w") ?? 0);

    private static Cell Cell(XElement tc, Edges edges, TableStyle style, Func<XElement, IReadOnlyList<Block>> blocks)
    {
        var tcPr = tc.Element(W + "tcPr");
        var own = tcPr?.Element(W + "tcBorders");
        Border? Side(string side, string inside, bool edge)
        {
            // Newer documents name the left and right sides start and end.
            var other = side switch { "left" => "start", "right" => "end", _ => side };
            var outer = edge ? style.Property("tblBorders", side) ?? style.Property("tblBorders", other) : style.Property("tblBorders", inside);
            return BorderOf(own?.Element(W + side) ?? own?.Element(W + other) ?? outer);
        }
        var borders = new CellBorders(
            Side("top", "insideH", edges.Top), Side("left", "insideV", edges.Left), Side("bottom", "insideH", edges.Bottom), Side("right", "insideV", edges.Right));
        var span = (int)Number(Val(tcPr?.Element(W + "gridSpan")) ?? "1");
        return CellProperties.Read(new Cell(blocks(tc), span, borders), tcPr);
    }

    // A border's size is in eighths of a point.
    private static Border? BorderOf(XElement? e) =>
        e is null || Val(e) is "nil" or "none" ? null : new Border(Number((string?)e.Attribute(W + "sz") ?? "4") / 8);

    // Which edges of the table a cell is on.
    private sealed record Edges(bool Top, bool Bottom, bool Left = false, bool Right = false);
}
