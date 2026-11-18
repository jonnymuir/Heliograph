using Heliograph.Pdf.Reading;

namespace Heliograph.Pdf.Layout;

/// <summary>
/// The rules for where a paragraph's lines fall on pages: keep together, keep with the next paragraph's opening lines, and
/// widow and orphan control, as LibreOffice lays out a Word document.
/// </summary>
internal sealed class PageFit(PageSet pages, Footnotes notes)
{
    // Keep together, or keep with the first line of what follows: move to a new page when that cannot fit here but can there.
    public bool MustMove(Paragraph p, List<Line> lines, double before, Block? next)
    {
        var flow = p.Style.Flow;
        if (!pages.HasContent || !(flow.KeepLines || flow.KeepNext))
        {
            return false;
        }

        // Kept with the next, the space after it comes between it and the next one's opening lines, so it counts too.
        var total = before + lines.Sum(l => l.Height) + (flow.KeepNext && next is not null ? p.Style.Spacing.After + FirstLineHeight(next) : 0);
        return total > pages.Room && total <= pages.BodyBottom - pages.BodyTop;
    }
    // How many of the remaining lines go on this page, leaving no lone first line at its foot or lone last line at the next page's head.
    public int Fit(List<Line> lines, int placed, bool widowControl)
    {
        var fit = 0;
        var y = pages.Y;
        // A line that refers to a footnote fits only with its note below.
        while (placed + fit < lines.Count && y + lines[placed + fit].Height - lines[placed + fit].Hang <= pages.BodyBottom - notes.Cost(lines.Skip(placed).Take(fit + 1)) + 0.01)
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
    // What must open a paragraph where it starts: all of it when it keeps its lines together, else its first line, or its
    // first two when it has more than one and widow and orphan control keeps a lone first line off the foot of a page.
    private double OpeningLines(Paragraph p)
    {
        var lines = LineBreaker.Break(p, pages.Geometry.ContentWidth, _ => "");
        var opening = p.Style.Flow.KeepLines ? lines.Count : p.Style.Flow.WidowControl && lines.Count > 1 ? 2 : 1;
        return lines.Take(opening).Sum(l => l.Height);
    }
}
