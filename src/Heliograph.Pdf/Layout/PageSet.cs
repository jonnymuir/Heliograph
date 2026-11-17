using Heliograph.Pdf.Reading;

namespace Heliograph.Pdf.Layout;

/// <summary>The pages laid out so far, the current one, and how far down its text area the next line goes.</summary>
internal sealed class PageSet(PageGeometry geometry, double bodyTop, double bodyBottom, int maxPages, CancellationToken cancellation)
{
    private readonly List<LaidOutPage> _pages = [];

    public PageGeometry Geometry { get; } = geometry;
    public double BodyTop { get; } = bodyTop;
    public double BodyBottom { get; } = bodyBottom;
    public IReadOnlyList<LaidOutPage> Pages => _pages;
    public LaidOutPage Current { get; private set; } = null!;
    public double Y { get; set; }
    public bool HasContent { get; set; }
    public bool OnFirstPage => _pages.Count == 1;
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
        Y = BodyTop;
        HasContent = false;
    }
}

/// <summary>A letter ran past the most pages a conversion allows.</summary>
internal sealed class PageLimitException(int pages) : Exception($"The letter runs past {pages} pages, the most a conversion allows.");
