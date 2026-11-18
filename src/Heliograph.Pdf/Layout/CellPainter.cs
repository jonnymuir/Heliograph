using Heliograph.Pdf.Reading;

namespace Heliograph.Pdf.Layout;

/// <summary>Where a row is drawn: on which page, from which left edge, and from how far down.</summary>
internal readonly record struct Canvas(LaidOutPage Page, double Left, double Top);

/// <summary>
/// Draws one cell of a table row: its shading, its lines and its borders. A table inside the cell is one of its lines,
/// drawn where that line falls. On a page, a line's notes go on the page with it, unless the row is a heading drawn again
/// on a later page.
/// </summary>
internal sealed class CellPainter(PageSet pages, PageNotes notes)
{
    /// <summary>One cell's shading, its lines from one index to another, moved down by an offset, and its borders, at the page's current position.</summary>
    public void Draw(RowLayout cells, int c, int from, int end, double height, double down)
    {
        Paint(new Canvas(pages.Current, pages.Geometry.Left, pages.Y), cells, c, (from, end), (height, down));
        foreach (var (_, line, owner) in cells[c].Lines.Skip(from).Take(end - from).Where(l => l.Owner.Node is not null))
        {
            notes.Place(line, owner.Node);
        }
    }

    /// <summary>One cell drawn on a canvas, its lines from one index to another, in a row this high, moved down this far.</summary>
    public static void Paint(Canvas at, RowLayout cells, int c, (int From, int End) span, (double Height, double Down) row)
    {
        var cell = cells[c];
        var (left, offset) = (at.Left + cell.Left, (span.From < cell.Lines.Count ? cell.Lines[span.From].Top : 0) - row.Down);
        if (cells.Source[c].Background is { } shading)
        {
            at.Page.Ops.Add(new FillOp(left, at.Top, cell.Width, row.Height, shading));
        }

        for (var k = span.From; k < span.End; k++)
        {
            var (top, line, owner) = cell.Lines[k];
            var (x, y) = (at.Left + cell.TextLeft, at.Top + cells.Above + (top - offset));
            if (line.Block is { } block)
            {
                at.Page.Ops.AddRange(block.Select(op => Shift.Moved(op, x, y)));
                continue;
            }

            LinePlacer.Place(at.Page, line, x, y, owner);
        }

        Borders(at, cell.Borders, (left, cell.Width), row.Height);
    }

    private static void Borders(Canvas at, CellBorders b, (double X, double Width) across, double height)
    {
        var (top, ops, (x, width)) = (at.Top, at.Page.Ops, across);
        if (b.Top is { } t)
        {
            ops.Add(new LineOp(x, top, x + width, top, t.Width));
        }

        if (b.Bottom is { } bottom)
        {
            ops.Add(new LineOp(x, top + height, x + width, top + height, bottom.Width));
        }

        if (b.Left is { } left)
        {
            ops.Add(new LineOp(x, top, x, top + height, left.Width));
        }

        if (b.Right is { } right)
        {
            ops.Add(new LineOp(x + width, top, x + width, top + height, right.Width));
        }
    }
}
