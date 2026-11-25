using Heliograph.Pdf.Reading;

namespace Heliograph.Pdf.Layout;

/// <summary>
/// Columns balanced before a continuous section break, as Word balances them: the section's last page has its columns
/// as short as they can be and still hold the rest of its text, so they end about level and the next section goes on
/// below them. The section is tried at heights in turn, on copies of the page set that nothing is kept from.
/// </summary>
internal static class ColumnBalance
{
    /// <summary>Sets where the columns of a section's last page end, when the section after goes on down that page.</summary>
    public static void Set(PageSet pages, WordDocument document, Section section, Section? next)
    {
        if (section.Columns is null || next is not { Start: SectionStart.Continuous } || next.Page.Width != section.Page.Width || next.Page.Height != section.Page.Height)
        {
            return;
        }

        var free = Run(pages, document, section, null);
        var last = free.Pages.Count - 1;
        var (top, bottom) = (free.ColumnsTop, free.BodyBottom);
        // The shortest columns that still hold the rest of the text on that page, to a tenth of a point.
        while (bottom - top > 0.1)
        {
            var middle = (top + bottom) / 2;
            (top, bottom) = Run(pages, document, section, (last, middle)).Pages.Count - 1 == last ? (top, middle) : (middle, bottom);
        }

        pages.BalanceAt(last, bottom);
    }

    // The section laid out on a copy of the page set, with its columns on one of the pages from now ending at a height.
    private static PageSet Run(PageSet pages, WordDocument document, Section section, (int Page, double Bottom)? balance)
    {
        var trial = pages.Trial();
        if (balance is { } b)
        {
            trial.BalanceAt(b.Page, b.Bottom);
        }

        var root = new StructNode("Document", null);
        new BodyFlow(trial, root, new PageNotes(new Footnotes(document, trial), new Endnotes(document, trial, root))).Run(section.Body);
        return trial;
    }
}
