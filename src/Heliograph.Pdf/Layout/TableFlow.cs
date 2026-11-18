using Heliograph.Pdf.Reading;

namespace Heliograph.Pdf.Layout;

/// <summary>
/// Lays out a table row by row. As in Word, a row that does not fit breaks across the page: each cell carries on from its
/// own last line that fits, under the heading rows drawn again as artifacts. A heading row, or one marked not to break,
/// moves whole to the next page instead. The text sits at the table's indent, the border one cell padding to its left.
/// A row's footnotes take room on its page: all those still to come in it, while working out how much of it fits.
/// </summary>
internal sealed class TableFlow(PageSet pages, PageNotes notes)
{
    private readonly CellPainter _cells = new(pages, notes);

    public void Run(Table t, StructNode node)
    {
        var headings = t.Rows.TakeWhile(r => r.Header).ToList();
        RowLayout? last = null;
        for (var r = 0; r < t.Rows.Count; r++)
        {
            var end = MergedRows.End(t.Rows, r);
            if (end > r)
            {
                var group = Enumerable.Range(r, end - r + 1).Select(k => RowLayout.Of(t, t.Rows[k], node.Add("TR"), pages.Geometry.ContentWidth)).ToList();
                PlaceMerged(t, group, headings, t.Rows[r]);
                (last, r) = (group[^1], end);
                continue;
            }

            last = RowLayout.Of(t, t.Rows[r], node.Add("TR"), pages.Geometry.ContentWidth);
            Place(t, t.Rows[r], last, headings);
        }

        // The table's bottom border sits below its last row.
        pages.Y += last?.BottomBorder ?? 0;
    }

    // Places a row: whole if it fits, else broken across pages where it can be, else moved whole to the next page.
    private void Place(Table t, Row row, RowLayout cells, List<Row> headings)
    {
        var from = new int[cells.Count];
        while (cells.Need(from) > Room(cells, from) + 0.01)
        {
            var cut = Breakable(row, from) ? cells.Cut(from, Room(cells, from)) : null;
            if (cut is null && pages.HasContent)
            {
                NewPage(t, headings, row);
                continue;
            }

            // Nothing fits even on an empty page: place what fits, at least a line of each cell, and go on.
            cut ??= cells.Cut(from, Room(cells, from), atLeastOne: true);
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

    // Rows joined by cells merged down a column, kept together on a page, each merged cell drawn once down them all.
    private void PlaceMerged(Table t, List<RowLayout> group, List<Row> headings, Row first)
    {
        var heights = MergedRows.Heights(group);
        if (Moves(heights.Sum() + notes.Footnotes.Cost(group.SelectMany(Lines))))
        {
            NewPage(t, headings, first);
        }

        for (var k = 0; k < group.Count; k++)
        {
            var cells = group[k];
            for (var c = 0; c < cells.Count; c++)
            {
                if (cells.Source[c].VMerge != "continue")
                {
                    var height = cells.Source[c].VMerge == "restart" ? MergedRows.Down(group, heights, k, c) : heights[k];
                    _cells.Draw(cells, c, 0, cells[c].Lines.Count, height, cells.Offset(c, height));
                }
            }

            pages.Y += heights[k];
            pages.HasContent = true;
        }
    }

    // The room on this page for the rest of a row, less what its footnotes still to come would take.
    private double Room(RowLayout cells, int[] from) =>
        pages.Room - notes.Footnotes.Cost(Enumerable.Range(0, cells.Count).SelectMany(c => cells[c].Lines.Skip(from[c]).Select(l => l.Line)));

    private static IEnumerable<Line> Lines(RowLayout cells) => Enumerable.Range(0, cells.Count).SelectMany(c => cells[c].Lines.Select(l => l.Line));

    // Whether rows kept together go to the next page: they do not fit here, but would on a page of their own.
    private bool Moves(double height) => pages.HasContent && height > pages.Room + 0.01 && height <= pages.BodyBottom - pages.BodyTop;

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
                var cells = RowLayout.Of(t, heading, null, pages.Geometry.ContentWidth);
                var from = new int[cells.Count];
                Draw(cells, from, null, cells.Need(from));
            }
        }
    }

    // Draws each cell's lines from one index up to another (or to the end), and the cell borders round that part. In a row
    // placed whole, a cell's text sits at its top, middle or foot; in a row broken across pages, at its top, as LibreOffice
    // lays it out.
    private void Draw(RowLayout cells, int[] from, int[]? to, double height)
    {
        var whole = to is null && from.All(f => f == 0);
        for (var c = 0; c < cells.Count; c++)
        {
            _cells.Draw(cells, c, from[c], to?[c] ?? cells[c].Lines.Count, height, whole ? cells.Offset(c, height) : 0);
        }

        pages.Y += height;
        pages.HasContent = true;
    }
}
