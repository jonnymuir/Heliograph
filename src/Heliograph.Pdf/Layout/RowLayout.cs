using Heliograph.Pdf.Reading;

namespace Heliograph.Pdf.Layout;

/// <summary>
/// A row's cells laid out as lines, so the row can be measured whole or from where a page broke it, and cut where a page
/// ends. Above is the space over each cell's text: the padding and the top border.
/// </summary>
internal sealed class RowLayout
{
    private readonly List<CellLayout> _cells;
    private readonly double _below;
    private readonly Row _row;
    private readonly double _topBorder;

    private RowLayout(List<CellLayout> cells, (double Padding, double Border) above, double below, Row row)
    {
        _cells = cells;
        Above = above.Padding + above.Border;
        _topBorder = above.Border;
        _below = below;
        _row = row;
    }

    /// <summary>The widest bottom border of the row's cells, which the last row of a table adds below itself.</summary>
    public double BottomBorder => _cells.Max(c => c.Borders.Bottom?.Width ?? 0);

    public double Above { get; }
    public int Count => _cells.Count;
    public CellLayout this[int c] => _cells[c];

    /// <summary>A row laid out in a text area this wide, across which the table is aligned.</summary>
    public static RowLayout Of(Table t, Row row, StructNode? rowNode, double area = 0)
    {
        var x = TableLeft(t, area);
        var column = 0;
        var cells = new List<CellLayout>();
        var topBorder = 0.0;
        foreach (var cell in row.Cells)
        {
            var width = t.Grid.Skip(column).Take(cell.Span).Sum();
            column += cell.Span;
            var cellNode = rowNode?.Add(row.Header ? "TH" : "TD", row.Header ? "Column" : null, cell.Span);
            cells.Add(Cell(t, cell, x, width, cellNode));
            topBorder = Math.Max(topBorder, cell.Borders.Top?.Width ?? 0);
            x += width;
        }

        return new RowLayout(cells, (t.Margins.Top, topBorder), t.Margins.Bottom, row);
    }

    // Where the table's left border goes: at its indent, a cell's padding left of the text, or centred or to the right.
    private static double TableLeft(Table t, double width) => t.Align switch
    {
        "center" when width > 0 => (width - t.Grid.Sum()) / 2,
        "right" when width > 0 => width - t.Grid.Sum(),
        _ => t.Indent - t.Margins.Left,
    };

    /// <summary>How far down a whole row of this height a cell's text starts: at the top, the middle or the bottom.</summary>
    public double Offset(int c, double height) => _cells[c].VAlign switch
    {
        "center" => Math.Max(0, (height - Natural(c)) / 2),
        "bottom" => Math.Max(0, height - Natural(c)),
        _ => 0,
    };

    /// <summary>The height a cell's text needs, with the row's padding and top border.</summary>
    public double Natural(int c) => Above + _cells[c].Height + _below;

    // The cell's text area is the cell less its padding and half of each side border.
    private static CellLayout Cell(Table t, Cell cell, double x, double width, StructNode? cellNode)
    {
        var text = width - t.Margins.Left - t.Margins.Right - (((cell.Borders.Left?.Width ?? 0) + (cell.Borders.Right?.Width ?? 0)) / 2);
        var lines = new List<(double, Line, Owner)>();
        var y = 0.0;
        foreach (var block in cell.Blocks)
        {
            if (block is Table inner)
            {
                var nested = NestedTable.Lay(inner, text, cellNode?.Add("Table"));
                lines.Add((y, nested, Owner.Artifact));
                y += nested.Height;
                continue;
            }

            if (block is not Paragraph p)
            {
                continue;
            }

            y += p.Style.SpaceAbove;
            var owner = cellNode is null ? Owner.Artifact : new Owner(cellNode.Add("P"), null);
            foreach (var line in LineBreaker.Break(p, text, _ => ""))
            {
                lines.Add((y, line, owner));
                y += line.Height;
            }

            y += p.Style.Spacing.After;
        }

        return new CellLayout(x, width, x + t.Margins.Left, cell.Borders, lines, y) { VAlign = cell.VAlign };
    }

    /// <summary>The height the row needs from these lines on, up to these lines (or to the end).</summary>
    public double Need(int[] from, int[]? to = null)
    {
        var content = 0.0;
        for (var c = 0; c < _cells.Count; c++)
        {
            var cell = _cells[c];
            var end = to?[c] ?? cell.Lines.Count;
            var start = from[c] < cell.Lines.Count ? cell.Lines[from[c]].Top : cell.Height;
            var bottom = end >= cell.Lines.Count ? cell.Height : cell.Lines[end].Top;
            content = Math.Max(content, bottom - start);
        }

        return Settle(Above + content + _below, to is null && from.All(f => f == 0));
    }

    /// <summary>The height the whole row needs for its cells that are not merged down a column.</summary>
    public double OwnNeed() =>
        Settle(Above + Enumerable.Range(0, _cells.Count).Where(c => _row.Cells[c].VMerge == "").Select(c => _cells[c].Height).DefaultIfEmpty(0).Max() + _below, whole: true);

    /// <summary>The row's cells as read: how each is merged down its column, its shading.</summary>
    public IReadOnlyList<Cell> Source => _row.Cells;

    /// <summary>The grid column each cell starts in.</summary>
    public IReadOnlyList<int> Columns => [.. _row.Cells.Select((_, c) => _row.Cells.Take(c).Sum(cell => cell.Span))];

    // A whole row is exactly the height it sets, or at least that height below its top border, as LibreOffice lays it out.
    private double Settle(double need, bool whole) =>
        !whole || _row.Height <= 0 ? need : _row.ExactHeight ? _row.Height : Math.Max(need, _row.Height + _topBorder);

    /// <summary>
    /// Where each cell's lines stop to fit in the room left: after its last line whose foot is within it. Null when no
    /// cell can place a line, unless at least one line of each is asked for.
    /// </summary>
    public int[]? Cut(int[] from, double room, bool atLeastOne = false)
    {
        var cut = new int[_cells.Count];
        var any = false;
        for (var c = 0; c < _cells.Count; c++)
        {
            var cell = _cells[c];
            var start = from[c] < cell.Lines.Count ? cell.Lines[from[c]].Top : 0;
            var end = from[c];
            while (end < cell.Lines.Count && Above + (cell.Lines[end].Top - start) + cell.Lines[end].Line.Height + _below <= room + 0.01)
            {
                end++;
            }

            if (atLeastOne && end == from[c] && end < cell.Lines.Count)
            {
                end++;
            }

            any |= end > from[c];
            cut[c] = end;
        }

        return any ? cut : null;
    }
}
