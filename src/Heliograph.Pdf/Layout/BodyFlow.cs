using Heliograph.Pdf.Reading;

namespace Heliograph.Pdf.Layout;

/// <summary>
/// Lays out a document's body on pages: paragraphs split with widow, orphan, keep-with-next and keep-together rules,
/// tables row by row, and the structure a screen reader follows built as it goes.
/// </summary>
internal sealed class BodyFlow(PageSet pages, StructNode root)
{
    private readonly TableFlow _tables = new(pages);
    private readonly ListNesting _lists = new(root);

    public void Run(IReadOnlyList<Block> blocks)
    {
        pages.NewPage();
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
                    _tables.Run(t, root.Add("Table"));
                    break;
            }
        }
    }

    private void Paragraph(Paragraph p, Owner owner, Block? next)
    {
        var s = p.Style;
        if (s.Flow.PageBreakBefore && pages.HasContent)
        {
            pages.NewPage();
        }

        var lines = LineBreaker.Break(p, pages.Geometry.ContentWidth, _ => "");
        // Space before is kept at the top of a page too, as LibreOffice lays out a Word document, less any part shared with
        // the space after the paragraph above, wherever that paragraph ended.
        var before = s.SpaceAbove;
        if (MustMove(p, lines, before, next))
        {
            pages.NewPage();
        }

        pages.Y += before;
        var placed = 0;
        while (placed < lines.Count)
        {
            var fit = Fit(lines, placed, s.Flow.WidowControl);
            for (var k = 0; k < fit; k++)
            {
                LinePlacer.Place(pages.Current, lines[placed + k], pages.Geometry.Left, pages.Y, owner);
                pages.Y += lines[placed + k].Height;
                pages.HasContent = true;
            }

            placed += fit;
            if (placed < lines.Count)
            {
                pages.NewPage();
            }
        }

        pages.Y = Math.Min(pages.Y + s.Spacing.After, pages.BodyBottom);
    }

    // Keep together, or keep with the first line of what follows: move to a new page when that cannot fit here but can there.
    private bool MustMove(Paragraph p, List<Line> lines, double before, Block? next)
    {
        var flow = p.Style.Flow;
        if (!pages.HasContent || !(flow.KeepLines || flow.KeepNext))
        {
            return false;
        }

        var total = before + lines.Sum(l => l.Height) + (flow.KeepNext && next is not null ? FirstLineHeight(next) : 0);
        return total > pages.Room && total <= pages.BodyBottom - pages.BodyTop;
    }

    // How many of the remaining lines go on this page, leaving no lone first line at its foot or lone last line at the next page's head.
    private int Fit(List<Line> lines, int placed, bool widowControl)
    {
        var fit = 0;
        var y = pages.Y;
        while (placed + fit < lines.Count && y + lines[placed + fit].Height - lines[placed + fit].Hang <= pages.BodyBottom + 0.01)
        {
            y += lines[placed + fit++].Height;
        }

        var remaining = lines.Count - placed;
        if (fit < remaining && widowControl)
        {
            // A lone last line takes one more with it; and if that leaves a lone first line here, the paragraph moves.
            if (remaining - fit == 1 && fit >= 2)
            {
                fit--;
            }

            if (fit == 1 && placed == 0)
            {
                fit = 0;
            }
        }

        return fit == 0 && !pages.HasContent ? 1 : fit;
    }

    private double FirstLineHeight(Block block) => block switch
    {
        Paragraph p => p.Style.SpaceAbove + OpeningLines(p),
        Table { Rows.Count: > 0 } t => TableFlow.RowHeight(t, t.Rows[0]),
        _ => 0,
    };

    // What must open a paragraph where it starts: its first line, or its first two when it has more than one and widow
    // and orphan control keeps a lone first line off the foot of a page.
    private double OpeningLines(Paragraph p)
    {
        var lines = LineBreaker.Break(p, pages.Geometry.ContentWidth, _ => "");
        return lines.Take(p.Style.Flow.WidowControl && lines.Count > 1 ? 2 : 1).Sum(l => l.Height);
    }
}
