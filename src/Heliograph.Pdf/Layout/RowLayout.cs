using Heliograph.Pdf.Reading;

namespace Heliograph.Pdf.Layout;

/// <summary>One cell of a row, broken into lines, each with its top measured from the top of the cell's text.</summary>
internal sealed record CellLayout(double Left, double Width, double TextLeft, CellBorders Borders, List<(double Top, Line Line, Owner Owner)> Lines, double Height);

/// <summary>
/// A row's cells laid out as lines, so the row can be measured whole or from where a page broke it, and cut where a page
/// ends. Above is the space over each cell's text: the padding and the top border.
/// </summary>
internal sealed class RowLayout
{
    private readonly List<CellLayout> _cells;
    private readonly double _below;

    private RowLayout(List<CellLayout> cells, double above, double below)
    {
        _cells = cells;
        Above = above;
        _below = below;
    }

    public double Above { get; }
    public int Count => _cells.Count;
    public CellLayout this[int c] => _cells[c];

    public static RowLayout Of(Table t, Row row, StructNode? rowNode)
    {
        var x = t.Indent - t.Margins.Left;
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

        return new RowLayout(cells, t.Margins.Top + topBorder, t.Margins.Bottom);
    }

    // The cell's text area is the cell less its padding and half of each side border.
    private static CellLayout Cell(Table t, Cell cell, double x, double width, StructNode? cellNode)
    {
        var text = width - t.Margins.Left - t.Margins.Right - (((cell.Borders.Left?.Width ?? 0) + (cell.Borders.Right?.Width ?? 0)) / 2);
        var lines = new List<(double, Line, Owner)>();
        var y = 0.0;
        foreach (var p in cell.Blocks.OfType<Paragraph>())
        {
            y += p.Style.Spacing.Before;
            var owner = cellNode is null ? Owner.Artifact : new Owner(cellNode.Add("P"), null);
            foreach (var line in LineBreaker.Break(p, text, _ => ""))
            {
                lines.Add((y, line, owner));
                y += line.Height;
            }

            y += p.Style.Spacing.After;
        }

        return new CellLayout(x, width, x + t.Margins.Left, cell.Borders, lines, y);
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

        return Above + content + _below;
    }

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
