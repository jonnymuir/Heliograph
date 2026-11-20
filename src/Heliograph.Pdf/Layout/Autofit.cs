using Heliograph.Pdf.Reading;

namespace Heliograph.Pdf.Layout;

/// <summary>
/// A table that fits its contents, as Word lays it out when a word is too wide for its column: the column widened to fit
/// that word, with its padding, the room taken from the other columns in proportion to what each can spare, so the table
/// keeps its width. A table of fixed layout keeps its columns as they are, and so does one with no word too wide.
/// </summary>
internal static class Autofit
{
    public static Table Widen(Table t)
    {
        if (t.Fixed || t.Grid.Count < 2)
        {
            return t;
        }

        var least = Least(t);
        var grid = t.Grid.ToArray();
        var short_ = Enumerable.Range(0, grid.Length).Sum(c => Math.Max(0, least[c] - grid[c]));
        var spare = Enumerable.Range(0, grid.Length).Sum(c => Math.Max(0, grid[c] - least[c]));
        if (short_ <= 0.01 || spare <= 0.01)
        {
            return t;
        }

        // Each narrow column takes the share of what is spare that it needs; each with room gives in proportion to its room.
        var taken = Math.Min(short_, spare);
        var widened = grid.Select((w, c) => least[c] > w
            ? w + ((least[c] - w) * taken / short_)
            : w - (Math.Max(0, w - least[c]) * taken / spare)).ToList();
        return t with { Grid = widened };
    }

    // The narrowest each column can be: its widest single cell's longest word, with the cell's padding.
    private static double[] Least(Table t)
    {
        var least = new double[t.Grid.Count];
        foreach (var row in t.Rows)
        {
            var column = row.GridBefore;
            foreach (var cell in row.Cells)
            {
                if (cell.Span == 1 && column < least.Length)
                {
                    var words = cell.Blocks.OfType<Paragraph>().Select(LineBreaker.Narrowest).DefaultIfEmpty(0).Max();
                    least[column] = Math.Max(least[column], words + t.Margins.Left + t.Margins.Right);
                }

                column += cell.Span;
            }
        }

        return least;
    }
}
