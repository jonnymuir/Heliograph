using System.Globalization;
using Heliograph.Pdf.Reading;

namespace Heliograph.Pdf.Layout;

/// <summary>Lays a whole document out: the body first, then each page's header and footer once the page count is known.</summary>
internal static class PageLayout
{
    public static (IReadOnlyList<LaidOutPage> Pages, StructNode Root) Run(WordDocument document, int maxPages, CancellationToken cancellation)
    {
        var g = document.Section.Page;
        var stories = document.Section.Stories;
        // As in Word, the text starts at the top margin, or below the page's header when the header reaches further down.
        TextArea Area(IReadOnlyList<Block> header, IReadOnlyList<Block> footer) => new(
            Math.Max(g.Top, g.HeaderDistance + Height(header, g)), g.Height - Math.Max(g.Bottom, g.FooterDistance + Height(footer, g)));
        var rest = Area(stories.Header, stories.Footer);
        var first = Area(stories.FirstHeader ?? stories.Header, stories.FirstFooter ?? stories.Footer);
        var pages = new PageSet(g, first, rest, maxPages, cancellation);
        var root = new StructNode("Document", null);
        new BodyFlow(pages, root).Run(document.Body);
        for (var n = 0; n < pages.Pages.Count; n++)
        {
            Decorate(pages.Pages[n], n, pages.Pages.Count, document.Section);
        }

        return (pages.Pages, root);
    }

    private static double Height(IReadOnlyList<Block> blocks, PageGeometry g) =>
        blocks.OfType<Paragraph>().Sum(p => p.Style.SpaceAbove + LineBreaker.Break(p, g.ContentWidth, _ => "9").Sum(l => l.Height) + p.Style.Spacing.After);

    private static void Decorate(LaidOutPage page, int index, int count, Section section)
    {
        var (g, stories) = (section.Page, section.Stories);
        var first = index == 0;
        var header = first && stories.FirstHeader is not null ? stories.FirstHeader : stories.Header;
        var footer = first && stories.FirstFooter is not null ? stories.FirstFooter : stories.Footer;
        string Field(string name) => name switch
        {
            "PAGE" => (index + 1).ToString(CultureInfo.InvariantCulture),
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
