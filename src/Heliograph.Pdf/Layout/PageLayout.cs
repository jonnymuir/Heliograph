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
        Footnotes? notes = null;
        foreach (var section in document.Sections)
        {
            var (g, stories) = (section.Page, section.Stories);
            pages.StartSection(section, Area(stories.FirstHeader ?? stories.Header, stories.FirstFooter ?? stories.Footer, g), Area(stories.Header, stories.Footer, g));
            notes ??= new Footnotes(document, pages);
            new BodyFlow(pages, root, notes).Run(section.Body);
        }

        pages.Finish();
        foreach (var page in pages.Pages)
        {
            Decorate(page, pages.Pages.Count);
        }

        return (pages.Pages, root);
    }

    // As in Word, the text starts at the top margin, or below the page's header when the header reaches further down.
    private static TextArea Area(IReadOnlyList<Block> header, IReadOnlyList<Block> footer, PageGeometry g) => new(
        Math.Max(g.Top, g.HeaderDistance + Height(header, g)), g.Height - Math.Max(g.Bottom, g.FooterDistance + Height(footer, g)));

    private static double Height(IReadOnlyList<Block> blocks, PageGeometry g) =>
        blocks.OfType<Paragraph>().Sum(p => p.Style.SpaceAbove + LineBreaker.Break(p, g.ContentWidth, _ => "9").Sum(l => l.Height) + p.Style.Spacing.After);

    private static void Decorate(LaidOutPage page, int count)
    {
        var (g, stories) = (page.Section.Page, page.Section.Stories);
        var header = page.FirstInSection && stories.FirstHeader is not null ? stories.FirstHeader : stories.Header;
        var footer = page.FirstInSection && stories.FirstFooter is not null ? stories.FirstFooter : stories.Footer;
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

        y = g.Height - g.FooterDistance - footer.OfType<Paragraph>().Sum(p => p.Style.SpaceAbove + LineBreaker.Break(p, g.ContentWidth, Field).Sum(l => l.Height) + p.Style.Spacing.After);
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
