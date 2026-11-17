using Heliograph.Pdf.Reading;

namespace Heliograph.Pdf.Layout;

/// <summary>
/// Lays out a table row by row. A row that does not fit starts a new page, where the heading rows are drawn again as
/// artifacts. As in Word and LibreOffice, the text sits at the table's indent and the border one cell padding to its left.
/// </summary>
internal sealed class TableFlow(PageSet pages)
{
    public void Run(Table t, StructNode node)
    {
        var headings = t.Rows.TakeWhile(r => r.Header).ToList();
        foreach (var row in t.Rows)
        {
            if (RowHeight(t, row) > pages.Room && pages.HasContent)
            {
                pages.NewPage();
                if (!row.Header)
                {
                    headings.ForEach(h => Draw(t, h, null));
                }
            }

            Draw(t, row, node.Add("TR"));
        }
    }

    public static double RowHeight(Table t, Row row)
    {
        var height = 0.0;
        foreach (var (cell, width) in Columns(t, row))
        {
            var text = TextWidth(t, cell, width);
            var h = t.Margins.Top + t.Margins.Bottom + (cell.Borders.Top?.Width ?? 0);
            foreach (var p in cell.Blocks.OfType<Paragraph>())
            {
                h += p.Style.Spacing.Before + LineBreaker.Break(p, text, _ => "").Sum(l => l.Height) + p.Style.Spacing.After;
            }

            height = Math.Max(height, h);
        }

        return height;
    }

    private void Draw(Table t, Row row, StructNode? rowNode)
    {
        var height = RowHeight(t, row);
        var x = pages.Geometry.Left + t.Indent - t.Margins.Left;
        foreach (var (cell, width) in Columns(t, row))
        {
            var cellNode = rowNode?.Add(row.Header ? "TH" : "TD", row.Header ? "Column" : null, cell.Span);
            var y = pages.Y + t.Margins.Top + (cell.Borders.Top?.Width ?? 0);
            foreach (var p in cell.Blocks.OfType<Paragraph>())
            {
                y += p.Style.Spacing.Before;
                var owner = cellNode is null ? Owner.Artifact : new Owner(cellNode.Add("P"), null);
                foreach (var line in LineBreaker.Break(p, TextWidth(t, cell, width), _ => ""))
                {
                    LinePlacer.Place(pages.Current, line, x + t.Margins.Left, y, owner);
                    y += line.Height;
                }

                y += p.Style.Spacing.After;
            }

            Borders(cell.Borders, x, width, height);
            x += width;
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

    private static IEnumerable<(Cell Cell, double Width)> Columns(Table t, Row row)
    {
        var column = 0;
        foreach (var cell in row.Cells)
        {
            yield return (cell, t.Grid.Skip(column).Take(cell.Span).Sum());
            column += cell.Span;
        }
    }

    // A cell's text area is the cell less its padding and half of each side border.
    private static double TextWidth(Table t, Cell cell, double width) =>
        width - t.Margins.Left - t.Margins.Right - (((cell.Borders.Left?.Width ?? 0) + (cell.Borders.Right?.Width ?? 0)) / 2);
}
