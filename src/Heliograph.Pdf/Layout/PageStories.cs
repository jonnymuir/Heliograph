using Heliograph.Pdf.Reading;

namespace Heliograph.Pdf.Layout;

/// <summary>
/// Which header and footer a page has: the first page's when its section gives the first page its own, the even pages'
/// on an even-numbered page when the document has different ones there, else the section's usual ones. And the text
/// area they leave.
/// </summary>
internal static class PageStories
{
    public static (IReadOnlyList<Block> Header, IReadOnlyList<Block> Footer) For(Section section, bool first, int number)
    {
        var stories = section.Stories;
        if (first && stories.FirstHeader is not null)
        {
            return (stories.FirstHeader, stories.FirstFooter ?? stories.Footer);
        }

        return number % 2 == 0 && stories.EvenHeader is not null ? (stories.EvenHeader, stories.EvenFooter ?? stories.Footer) : (stories.Header, stories.Footer);
    }

    /// <summary>As in Word, the text starts at the top margin, or below the header when the header reaches further down.</summary>
    public static TextArea Area((IReadOnlyList<Block> Header, IReadOnlyList<Block> Footer) stories, PageGeometry g) => new(
        Math.Max(g.Top, g.HeaderDistance + Height(stories.Header, g)), g.Height - Math.Max(g.Bottom, g.FooterDistance + Height(stories.Footer, g)));

    public static double Height(IReadOnlyList<Block> blocks, PageGeometry g, Func<FieldInline, string>? field = null) =>
        blocks.Sum(b => b switch
        {
            Paragraph p => p.Style.SpaceAbove + LineBreaker.Break(p, g.ContentWidth, field ?? (_ => "9")).Sum(l => l.Height) + p.Style.Spacing.After,
            Table t => NestedTable.Lay(t, g.ContentWidth, null).Height,
            _ => 0,
        });
}
