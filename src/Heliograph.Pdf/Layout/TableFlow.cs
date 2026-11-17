using Heliograph.Pdf.Reading;

namespace Heliograph.Pdf.Layout;

/// <summary>
/// Lays out a table row by row. As in Word, a row that does not fit breaks across the page: each cell carries on from its
/// own last line that fits, under the heading rows drawn again as artifacts. A heading row, or one marked not to break,
/// moves whole to the next page instead. The text sits at the table's indent, the border one cell padding to its left.
/// </summary>
internal sealed class TableFlow(PageSet pages)
{
    public void Run(Table t, StructNode node)
    {
        var headings = t.Rows.TakeWhile(r => r.Header).ToList();
        foreach (var row in t.Rows)
        {
            Place(t, row, RowLayout.Of(t, row, node.Add("TR")), headings);
        }
    }

    // Places a row: whole if it fits, else broken across pages where it can be, else moved whole to the next page.
    private void Place(Table t, Row row, RowLayout cells, List<Row> headings)
    {
        var from = new int[cells.Count];
        while (cells.Need(from) > pages.Room + 0.01)
        {
            var cut = Breakable(row, from) ? cells.Cut(from, pages.Room) : null;
            if (cut is null && pages.HasContent)
            {
                NewPage(t, headings, row);
                continue;
            }

            // Nothing fits even on an empty page: place what fits, at least a line of each cell, and go on.
            cut ??= cells.Cut(from, pages.Room, atLeastOne: true);
            if (cut is null)
            {
                break;
            }

            Draw(cells, from, cut, cells.Need(from, cut));
            from = cut;
            NewPage(t, headings, row);
        }

        Draw(cells, from, null, cells.Need(from));
    }

    // A heading row never breaks; a row marked not to break may still have to, once it has started on a page of its own.
    private static bool Breakable(Row row, int[] from) => !row.Header && !(row.CantSplit && from.All(f => f == 0));

    /// <summary>The height of a whole row, when nothing of it has been placed yet.</summary>
    public static double RowHeight(Table t, Row row) => RowLayout.Of(t, row, null).Need(new int[row.Cells.Count]);

    private void NewPage(Table t, List<Row> headings, Row row)
    {
        pages.NewPage();
        if (!row.Header)
        {
            foreach (var heading in headings)
            {
                var cells = RowLayout.Of(t, heading, null);
                var from = new int[cells.Count];
                Draw(cells, from, null, cells.Need(from));
            }
        }
    }

    // Draws each cell's lines from one index up to another (or to the end), and the cell borders round that part.
    private void Draw(RowLayout cells, int[] from, int[]? to, double height)
    {
        for (var c = 0; c < cells.Count; c++)
        {
            var cell = cells[c];
            var end = to?[c] ?? cell.Lines.Count;
            var offset = from[c] < cell.Lines.Count ? cell.Lines[from[c]].Top : 0;
            for (var k = from[c]; k < end; k++)
            {
                var (top, line, owner) = cell.Lines[k];
                LinePlacer.Place(pages.Current, line, pages.Geometry.Left + cell.TextLeft, pages.Y + cells.Above + (top - offset), owner);
            }

            Borders(cell.Borders, pages.Geometry.Left + cell.Left, cell.Width, height);
        }

        pages.Y += height;
        pages.HasContent = true;
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
