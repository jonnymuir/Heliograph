using Heliograph.Pdf.Reading;

namespace Heliograph.Pdf.Layout;

/// <summary>Where a page's text area starts and ends: below its header and above its footer, or at the margins.</summary>
internal sealed record TextArea(double Top, double Bottom);

/// <summary>
/// The pages laid out so far, the current one, and how far down its text area the next line goes. Each section starts a
/// page of its own, with its page size and margins; its first page has a text area of its own when it has its own header
/// or footer. Each page carries its number as the section counts it.
/// </summary>
internal sealed class PageSet(int maxPages, CancellationToken cancellation)
{
    private readonly List<LaidOutPage> _pages = [];
    private Section _section = null!;
    private bool _sectionStarts;
    private int _number;
    private double _areaBottom;

    public PageGeometry Geometry => _section.Page;
    public double BodyTop { get; private set; }

    /// <summary>Where the body's text may go down to: the foot of the text area, less the room its footnotes take.</summary>
    public double BodyBottom => _areaBottom - Reserved;

    /// <summary>The room at the foot of this page's text area kept for its footnotes.</summary>
    public double Reserved { get; set; }

    /// <summary>Called as each page is finished, with the foot of its text area: footnotes are drawn then.</summary>
    public Action<LaidOutPage, double>? Finishing { get; set; }

    public IReadOnlyList<LaidOutPage> Pages => _pages;
    public LaidOutPage Current { get; private set; } = null!;
    public double Y { get; set; }

    /// <summary>The boxes of this page's floating pictures that text wraps round.</summary>
    public List<Exclusion> Wraps { get; } = [];

    /// <summary>The foot of the text laid out last: its last line's, less any line spacing over single, or a table's.</summary>
    public double TextFoot { get; set; }
    public bool HasContent { get; set; }
    public double Room => BodyBottom - Y;

    /// <summary>Starts a section on a new page, numbered from where the section says or on from the page before.</summary>
    public void StartSection(Section section)
    {
        // The page before is finished, footnotes and all, on its own section's page.
        if (_pages.Count > 0)
        {
            Finish();
        }

        (_section, _sectionStarts) = (section, true);
        Open();
    }

    /// <summary>Finishes the last page.</summary>
    public void Finish() => Finishing?.Invoke(Current, _areaBottom);

    public void NewPage()
    {
        if (_pages.Count > 0)
        {
            Finish();
        }

        Open();
    }

    private void Open()
    {
        cancellation.ThrowIfCancellationRequested();
        if (_pages.Count >= maxPages)
        {
            throw new PageLimitException(maxPages);
        }

        _number = _sectionStarts && _section.Numbers.Start is { } start ? start : _number + 1;
        Current = new LaidOutPage { Section = _section, Number = _number, FirstInSection = _sectionStarts };
        _pages.Add(Current);
        var area = PageStories.Area(PageStories.For(_section, _sectionStarts, _number), Geometry);
        (BodyTop, _areaBottom) = (area.Top, area.Bottom);
        _sectionStarts = false;
        Y = TextFoot = BodyTop;
        Reserved = 0;
        Wraps.Clear();
        HasContent = false;
    }
}

/// <summary>A letter ran past the most pages a conversion allows.</summary>
internal sealed class PageLimitException(int pages) : Exception($"The letter runs past {pages} pages, the most a conversion allows.");
