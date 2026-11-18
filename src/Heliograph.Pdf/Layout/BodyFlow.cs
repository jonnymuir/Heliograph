using Heliograph.Pdf.Reading;

namespace Heliograph.Pdf.Layout;

/// <summary>
/// Lays out a document's body on pages: paragraphs split with widow, orphan, keep-with-next and keep-together rules,
/// tables row by row, and the structure a screen reader follows built as it goes.
/// </summary>
internal sealed class BodyFlow(PageSet pages, StructNode root, Footnotes notes)
{
    private readonly TableFlow _tables = new(pages);
    private readonly ListNesting _lists = new(root);
    private readonly PageFit _fit = new(pages, notes);
    private ParagraphFrame? _frame;

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
                    _frame = null;
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
        var placed = 0;
        while (placed < lines.Count)
        {
            var fit = _fit.Fit(lines, placed, s.Flow.WidowControl);
            for (var k = 0; k < fit; k++)
            {
                LinePlacer.Place(pages.Current, lines[placed + k], pages.Geometry.Left, pages.Y, owner);
                notes.Place(lines[placed + k], owner.Node);
                pages.Y += lines[placed + k].Height;
                pages.HasContent = true;
            }

            placed += fit;
            if (placed < lines.Count)
            {
                NextPage(box);
            }
        }

        Finish(p, box);
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
