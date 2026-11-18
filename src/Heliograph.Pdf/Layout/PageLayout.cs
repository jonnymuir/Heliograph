using System.Globalization;
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
        foreach (var section in document.Sections)
        {
            pages.StartSection(section);
            notes ??= new PageNotes(new Footnotes(document, pages), new Endnotes(document, pages, root));
            new BodyFlow(pages, root, notes).Run(section.Body);
        }

        notes?.Endnotes.Run();
        pages.Finish();
        foreach (var page in pages.Pages)
        {
            Decorate(page, pages.Pages.Count);
        }

        return (pages.Pages, root);
    }

    private static void Decorate(LaidOutPage page, int count)
    {
        var g = page.Section.Page;
        var (header, footer) = PageStories.For(page.Section, page.FirstInSection, page.Number);
        string Field(string name) => name switch
        {
            "PAGE" => ListLabels.Number(page.Number, page.Section.Numbers.Format),
            "NUMPAGES" => count.ToString(CultureInfo.InvariantCulture),
            _ => "",
        };

        var y = g.HeaderDistance;
        foreach (var p in header.OfType<Paragraph>())
        {
            y = Story(page, p, y, g, Field);
        }

        y = g.Height - g.FooterDistance - PageStories.Height(footer, g, Field);
        foreach (var p in footer.OfType<Paragraph>())
        {
            y = Story(page, p, y, g, Field);
        }
    }

    private static double Story(LaidOutPage page, Paragraph p, double y, PageGeometry g, Func<string, string> field)
    {
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
