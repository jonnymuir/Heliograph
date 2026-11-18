using Heliograph.Pdf.Reading;

namespace Heliograph.Pdf.Layout;

/// <summary>Draws one cell of a table row at the page's current position: its shading, its lines and its borders.</summary>
internal sealed class CellPainter(PageSet pages)
{
    // One cell's shading, its lines from one index to another, moved down by an offset, and its borders.
    public void Draw(RowLayout cells, int c, int from, int end, double height, double down)
    {
        var cell = cells[c];
        var (left, offset) = (pages.Geometry.Left + cell.Left, (from < cell.Lines.Count ? cell.Lines[from].Top : 0) - down);
        if (cells.Source[c].Background is { } shading)
        {
            pages.Current.Ops.Add(new FillOp(left, pages.Y, cell.Width, height, shading));
        }

        for (var k = from; k < end; k++)
        {
            var (top, line, owner) = cell.Lines[k];
            LinePlacer.Place(pages.Current, line, pages.Geometry.Left + cell.TextLeft, pages.Y + cells.Above + (top - offset), owner);
        }

        Borders(cell.Borders, left, cell.Width, height);
    }

    private void Borders(CellBorders b, double x, double width, double height)
    {
        var (top, ops) = (pages.Y, pages.Current.Ops);
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
