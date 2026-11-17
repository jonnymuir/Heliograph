using Heliograph.Pdf.Reading;

namespace Heliograph.Pdf.Layout;

/// <summary>Where a page's text area starts and ends: below its header and above its footer, or at the margins.</summary>
internal sealed record TextArea(double Top, double Bottom);

/// <summary>
/// The pages laid out so far, the current one, and how far down its text area the next line goes. The first page has a
/// text area of its own when it has its own header or footer.
/// </summary>
internal sealed class PageSet(PageGeometry geometry, TextArea first, TextArea rest, int maxPages, CancellationToken cancellation)
{
    private readonly List<LaidOutPage> _pages = [];

    public PageGeometry Geometry { get; } = geometry;
    public double BodyTop { get; private set; }
    public double BodyBottom { get; private set; }
    public IReadOnlyList<LaidOutPage> Pages => _pages;
    public LaidOutPage Current { get; private set; } = null!;
    public double Y { get; set; }
    public bool HasContent { get; set; }
    public double Room => BodyBottom - Y;

    public void NewPage()
    {
        cancellation.ThrowIfCancellationRequested();
        if (_pages.Count >= maxPages)
        {
            throw new PageLimitException(maxPages);
        }

        Current = new LaidOutPage();
        _pages.Add(Current);
        (BodyTop, BodyBottom) = _pages.Count == 1 ? (first.Top, first.Bottom) : (rest.Top, rest.Bottom);
        Y = BodyTop;
        HasContent = false;
    }
}

/// <summary>A letter ran past the most pages a conversion allows.</summary>
internal sealed class PageLimitException(int pages) : Exception($"The letter runs past {pages} pages, the most a conversion allows.");
