using Heliograph.Pdf.Reading;

namespace Heliograph.Pdf.Layout;

/// <summary>
/// Lays a whole document out: each section's body on pages of its own, then each page's header and footer once the page
/// count is known.
/// </summary>
internal static class PageLayout
{
    public static (IReadOnlyList<LaidOutPage> Pages, StructNode Root) Run(WordDocument document, int maxPages, CancellationToken cancellation)
    {
        var pages = new PageSet(maxPages, cancellation);
        var root = new StructNode("Document", null);
        PageNotes? notes = null;
        for (var i = 0; i < document.Sections.Count; i++)
        {
            var section = document.Sections[i];
            pages.StartSection(section);
            ColumnBalance.Set(pages, document, section, i + 1 < document.Sections.Count ? document.Sections[i + 1] : null);
            notes ??= new PageNotes(new Footnotes(document, pages), new Endnotes(document, pages, root));
            new BodyFlow(pages, root, notes).Run(section.Body);
        }

        notes?.Endnotes.Run();
        pages.Finish();
        foreach (var page in pages.Pages.Where(p => !p.Blank))
        {
            Decorate(page, pages.Pages);
        }

        return (pages.Pages, root);
    }

    private static void Decorate(LaidOutPage page, IReadOnlyList<LaidOutPage> all)
    {
        var g = page.Section.Page;
        var (header, footer) = PageStories.For(page.Section, page.FirstInSection, page.Number);
        string Field(FieldInline f) => Value(f, page, all);
        using var fields = FieldValues.Use(Field);

        var y = g.HeaderDistance;
        foreach (var block in header)
        {
            y = Story(page, block, y, g, Field);
        }

        y = g.Height - g.FooterDistance - PageStories.Height(footer, g, Field);
        foreach (var block in footer)
        {
            y = Story(page, block, y, g, Field);
        }
    }

    // A page number in its switch's format or else its section's; a count of pages in its switch's format or else figures.
    private static string Value(FieldInline f, LaidOutPage page, IReadOnlyList<LaidOutPage> all) => f.Field switch
    {
        "PAGE" => ListLabels.Number(page.Number, f.Format ?? page.Section.Numbers.Format),
        "NUMPAGES" => ListLabels.Number(all.Count, f.Format ?? "decimal"),
        "SECTIONPAGES" => ListLabels.Number(all.Count(p => p.Section == page.Section), f.Format ?? "decimal"),
        _ => "",
    };

    // A header's or footer's paragraph, or its table laid out as one block across the text, as a table in a cell is.
    private static double Story(LaidOutPage page, Block block, double y, PageGeometry g, Func<FieldInline, string> field)
    {
        if (block is Table t)
        {
            var table = NestedTable.Lay(t, g.ContentWidth, null);
            page.Ops.AddRange(table.Block!.Select(op => Shift.Moved(op, g.Left, y)));
            return y + table.Height;
        }

        if (block is not Paragraph p)
        {
            return y;
        }

        y += p.Style.SpaceAbove;
        FloatPlacer.Place(page, p, g, y, null);
        foreach (var line in LineBreaker.Break(p, g.ContentWidth, field))
        {
            LinePlacer.Place(page, line, g.Left, y, Owner.Artifact);
            y += line.Height;
        }

        return y + p.Style.Spacing.After;
    }
}
