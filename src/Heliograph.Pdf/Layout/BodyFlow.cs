using Heliograph.Pdf.Reading;

namespace Heliograph.Pdf.Layout;

/// <summary>
/// Lays out a document's body on pages: paragraphs split with widow, orphan, keep-with-next and keep-together rules,
/// tables row by row, and the structure a screen reader follows built as it goes.
/// </summary>
internal sealed class BodyFlow(PageSet pages, StructNode root, PageNotes notes)
{
    private readonly TableFlow _tables = new(pages, notes);
    private readonly ListNesting _lists = new(root);
    private readonly PageFit _fit = new(pages, notes.Footnotes);
    private ParagraphFrame? _frame;

    /// <summary>Lays out a section's blocks from where the page set stands.</summary>
    public void Run(IReadOnlyList<Block> blocks)
    {
        for (var i = 0; i < blocks.Count; i++)
        {
            var next = i + 1 < blocks.Count ? blocks[i + 1] : null;
            switch (blocks[i])
            {
                case Paragraph { IsListItem: true } p:
                    Paragraph(p, _lists.Item(p.ListLevel), next);
                    break;
                case Paragraph p:
                    _lists.Clear();
                    Paragraph(p, new Owner(root.Add(p.Style.OutlineLevel is { } level and < 6 ? $"H{level + 1}" : "P"), null), next);
                    break;
                case Table t:
                    _lists.Clear();
                    _frame = null;
                    Below(t);
                    _tables.Run(t, root.Add("Table"));
                    pages.TextFoot = pages.Y;
                    break;
            }
        }
    }

    private void Paragraph(Paragraph p, Owner owner, Block? next)
    {
        var s = p.Style;
        if (s.Flow.PageBreakBefore && pages.HasContent)
        {
            pages.BreakPage();
        }

        var lines = LineBreaker.Break(p, pages.Geometry.ContentWidth, pages.Field);
        // Space before is kept at the top of a page too, as LibreOffice lays out a Word document, less any part shared with
        // the space after the paragraph above, wherever that paragraph ended; but a paragraph a keep rule moves to a new
        // page starts at its top.
        var before = s.SpaceAbove;
        if (_fit.MustMove(p, lines, before, next))
        {
            pages.NewPage();
            before = 0;
        }

        var box = ParagraphBox.Of(p, pages, _frame, next);
        Open(box, before);
        FloatPlacer.Place(pages.Current, p, pages.Geometry, pages.Y, owner.Node);
        lines = Wrapped(p, lines);
        Place(lines, owner, box, s.Flow.WidowControl);
        Finish(p, box);
        if (lines[^1].PageBreakAfter)
        {
            Break(lines[^1]);
        }
    }

    // A paragraph's lines, as many on each page or column as fit, going on where the last run leaves off.
    private void Place(List<Line> lines, Owner owner, ParagraphBox? box, bool widowControl)
    {
        var placed = 0;
        while (placed < lines.Count)
        {
            var fit = UpToBreak(lines, placed, _fit.Fit(lines, placed, widowControl));
            foreach (var line in lines.GetRange(placed, fit))
            {
                LinePlacer.Place(pages.Current, line, pages.Geometry.Left, pages.Y, owner);
                notes.Place(line, owner.Node);
                pages.TextFoot = pages.Y + line.Height - line.Hang;
                pages.Y += line.Height;
                pages.HasContent = true;
            }

            placed += fit;
            if (placed < lines.Count)
            {
                Turn(fit > 0 ? lines[placed - 1] : null, box);
            }
        }
    }

    // Where the rest of a paragraph goes after a run of lines: on the next page after a page break, else in the next column
    // or on the next page, with the next part of its box.
    private void Turn(Line? last, ParagraphBox? box)
    {
        if (last is { PageBreakAfter: true, ColumnBreakAfter: false })
        {
            pages.BreakPage();
            return;
        }

        NextPage(box);
    }

    // A break that ends the paragraph: a column break goes on in the next column, a page break on the next page.
    private void Break(Line line)
    {
        if (line.ColumnBreakAfter)
        {
            pages.NewPage();
            return;
        }

        pages.BreakPage();
    }

    // A new page for the rest of the paragraph, with the next part of its box if it has one.
    private void NextPage(ParagraphBox? box)
    {
        if (box is null)
        {
            pages.NewPage();
            return;
        }

        box.Break();
    }

    // A table does not wrap round a floating picture: it starts below any it would meet, as LibreOffice lays it out.
    private void Below(Table t)
    {
        var height = t.Rows.Count > 0 ? TableFlow.RowHeight(t, t.Rows[0]) : 0;
        while (pages.Wraps.FirstOrDefault(e => e.Top < pages.Y + height && e.Bottom > pages.Y) is { } picture)
        {
            pages.Y = picture.Bottom;
        }
    }

    // The paragraph broken again round the floating pictures on its page that text wraps round, from where it starts.
    private List<Line> Wrapped(Paragraph p, List<Line> lines)
    {
        if (pages.Wraps.Count == 0)
        {
            return lines;
        }

        var g = pages.Geometry;
        return LineBreaker.Break(p, g.ContentWidth, pages.Field, new LineBounds(pages.Y, (top, height) => WrapRoom.Of(pages.Wraps, g.Left, (top, height), (0, g.ContentWidth))));
    }

    // As many of the lines that fit as come before a page break, the line it ends included; the rest go on the next page.
    private static int UpToBreak(List<Line> lines, int placed, int fit)
    {
        var at = lines.FindIndex(placed, fit, l => l.PageBreakAfter);
        return at < 0 ? fit : at - placed + 1;
    }

    private void Open(ParagraphBox? box, double before)
    {
        if (box is null)
        {
            pages.Y += before;
            return;
        }

        box.Open(before);
    }

    // The paragraph's space after, and its box drawn.
    private void Finish(Paragraph p, ParagraphBox? box)
    {
        if (box is null)
        {
            pages.Y = Math.Min(pages.Y + p.Style.Spacing.After, pages.BodyBottom);
        }
        else
        {
            box.Close(p.Style.Spacing.After);
        }

        _frame = p.Style.Frame;
    }

}
