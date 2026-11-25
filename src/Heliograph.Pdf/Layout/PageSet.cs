using Heliograph.Pdf.Reading;

namespace Heliograph.Pdf.Layout;

/// <summary>Where a page's text area starts and ends: below its header and above its footer, or at the margins.</summary>
internal sealed record TextArea(double Top, double Bottom);

/// <summary>
/// The pages laid out so far, the current one, and how far down its text area the next line goes. Each section starts a
/// page of its own, with its page size and margins, unless it goes on down the page before, or after a blank page when it
/// starts on an odd or even page; its first page has a text area of its own when it has its own header or footer. Each
/// page carries its number as the section counts it.
/// </summary>
internal sealed class PageSet(int maxPages, CancellationToken cancellation)
{
    private readonly List<LaidOutPage> _pages = [];
    private Section _section = null!;
    private bool _sectionStarts;
    private int _column;
    private double _deepest;
    private int _number;
    private double _areaBottom;
    private (int Page, double Bottom)? _balance;

    /// <summary>The text area being laid out: the page's, or, in a section set in columns, the column's.</summary>
    public PageGeometry Geometry => _section.Columns is { } set ? Column(set, _column) : _section.Page;
    public double BodyTop { get; private set; }

    /// <summary>Where this page's columns start: the top of its text, or where a section in columns starts down the page.</summary>
    public double ColumnsTop { get; private set; }

    /// <summary>
    /// Where the body's text may go down to: the foot of the text area, or higher on a page whose columns are balanced,
    /// less the room its footnotes take.
    /// </summary>
    public double BodyBottom => (_balance is { } b && b.Page == _pages.Count - 1 ? Math.Min(b.Bottom, _areaBottom) : _areaBottom) - Reserved;

    /// <summary>How much of the last paragraph's space after the foot of the text area cut off, until more is laid out.</summary>
    public double Cut { get; set; }

    /// <summary>The room at the foot of this page's text area kept for its footnotes.</summary>
    public double Reserved { get; set; }

    /// <summary>Called as each page is finished, with the foot of its text area: footnotes are drawn then.</summary>
    public Action<LaidOutPage, double>? Finishing { get; set; }

    public IReadOnlyList<LaidOutPage> Pages => _pages;
    public LaidOutPage Current { get; private set; } = null!;
    public double Y { get; set; }

    /// <summary>The boxes of this page's floating pictures that text wraps round.</summary>
    public List<Exclusion> Wraps => Current.Wraps;

    /// <summary>The foot of the text laid out last: its last line's, less any line spacing over single, or a table's.</summary>
    public double TextFoot { get; set; }
    public bool HasContent { get; set; }
    public double Room => BodyBottom - Y;

    /// <summary>
    /// A field in the body worked out as it is laid out: the page number, in its switch's format or its section's, of the
    /// page the paragraph starts on. Counts of pages are only known once the letter is laid out, so they are kept to
    /// headers and footers.
    /// </summary>
    public string Field(FieldInline f) =>
        f.Field == "PAGE" ? ListLabels.Number(Current.Number, f.Format ?? Current.Section.Numbers.Format) : "";

    /// <summary>Starts a section on a new page, numbered from where the section says or on from the page before.</summary>
    public void StartSection(Section section)
    {
        if (GoesOn(section))
        {
            Settle();
            _section = section;
            ColumnsTop = Y;
            return;
        }

        // The page before is finished, footnotes and all, on its own section's page; then, for a section that starts on an
        // odd or even page, a blank page when the next page's number is the other: as Word prints it, with nothing on it,
        // not even a header or footer, but counted.
        if (_pages.Count > 0)
        {
            Finish();
            if (NeedsBlank(section))
            {
                Open(blank: true);
                Finishing?.Invoke(Current, _areaBottom);
            }
        }

        (_section, _sectionStarts) = (section, true);
        Open();
    }

    // Whether the page this section would start on is numbered even when it should be odd, or odd when it should be
    // even. Word goes by the page's number, as it does for even pages' headers, so a section numbered from 1 again is
    // already on an odd page.
    private bool NeedsBlank(Section section)
    {
        var odd = (section.Numbers.Start ?? _number + 1) % 2 != 0;
        return (section.Start, odd) is (SectionStart.OddPage, false) or (SectionStart.EvenPage, true);
    }

    // A continuous section goes on down this page, with its own margins across it, when the page is the same size; as in
    // Word, one on a page of another size starts a new page.
    private bool GoesOn(Section section) =>
        section.Start == SectionStart.Continuous && _pages.Count > 0 && (section.Page.Width, section.Page.Height) == (_section.Page.Width, _section.Page.Height);

    /// <summary>The columns of this page's balanced text end at this height down the page, of the pages from now.</summary>
    public void BalanceAt(int page, double bottom) => _balance = (_pages.Count - 1 + page, bottom);

    /// <summary>
    /// A copy of where the page set stands, on a page of its own that nothing is kept from, for trying a section's layout
    /// before it is laid out.
    /// </summary>
    public PageSet Trial()
    {
        var trial = new PageSet(maxPages, cancellation)
        {
            _section = _section, _column = _column, _deepest = _deepest, _number = _number, _areaBottom = _areaBottom,
            BodyTop = BodyTop, ColumnsTop = ColumnsTop, Reserved = Reserved, Y = Y, TextFoot = TextFoot, HasContent = HasContent,
            Current = new LaidOutPage { Section = Current.Section, Number = Current.Number, FirstInSection = Current.FirstInSection },
        };
        trial.Current.Wraps.AddRange(Wraps);
        trial._pages.Add(trial.Current);
        return trial;
    }

    // Text in columns before a continuous section break ends below its deepest column, with the lines between them drawn
    // down to there; the next section goes on below it.
    private void Settle()
    {
        if (_section.Columns is not { } set)
        {
            return;
        }

        if (set.Separator)
        {
            Separators(set);
        }

        Y = TextFoot = Math.Max(_deepest, Y + Cut);
        (_column, _deepest, _balance) = (0, 0, null);
    }

    /// <summary>Finishes the last page: the lines between its columns, if its section asks, then its footnotes.</summary>
    public void Finish()
    {
        if (_section.Columns is { Separator: true } set)
        {
            Separators(set);
        }

        Finishing?.Invoke(Current, _areaBottom);
    }

    /// <summary>Goes on in the next column, or, in the last or only one, on a new page.</summary>
    public void NewPage()
    {
        if (_section.Columns is { } set && _column + 1 < set.Columns.Count)
        {
            _deepest = Math.Max(_deepest, Y + Cut);
            _column++;
            Y = TextFoot = ColumnsTop;
            HasContent = false;
            return;
        }

        BreakPage();
    }

    /// <summary>Goes on on a new page, whichever column this is: a page break.</summary>
    public void BreakPage()
    {
        if (_pages.Count > 0)
        {
            Finish();
        }

        Open();
    }

    // A column's text area: the page's, from the column's left edge, as wide as the column.
    private PageGeometry Column(ColumnSet set, int column)
    {
        var page = _section.Page;
        var left = page.Left + set.Columns.Take(column).Sum(c => c.Width + c.Space);
        return page with { Left = left, Right = page.Width - left - set.Columns[column].Width };
    }

    // A line down the middle of each space between columns, from their top to the foot of the deepest column.
    private void Separators(ColumnSet set)
    {
        var bottom = Math.Max(_deepest, Y);
        for (var c = 0; c < set.Columns.Count - 1; c++)
        {
            var x = Column(set, c).Left + set.Columns[c].Width + (set.Columns[c].Space / 2);
            Current.Ops.Add(new LineOp(x, ColumnsTop, x, bottom, 0.5));
        }
    }

    private void Open(bool blank = false)
    {
        cancellation.ThrowIfCancellationRequested();
        if (_pages.Count >= maxPages)
        {
            throw new PageLimitException(maxPages);
        }

        _number = _sectionStarts && _section.Numbers.Start is { } start ? start : _number + 1;
        Current = new LaidOutPage { Section = _section, Number = _number, FirstInSection = _sectionStarts, Blank = blank };
        _pages.Add(Current);
        var area = PageStories.Area(PageStories.For(_section, _sectionStarts, _number), _section.Page);
        (BodyTop, _areaBottom) = (area.Top, area.Bottom);
        ColumnsTop = BodyTop;
        (_sectionStarts, _column, _deepest) = (false, 0, 0);
        Y = TextFoot = BodyTop;
        Reserved = 0;
        HasContent = false;
    }
}

/// <summary>A letter ran past the most pages a conversion allows.</summary>
internal sealed class PageLimitException(int pages) : Exception($"The letter runs past {pages} pages, the most a conversion allows.");
