using Heliograph.Pdf.Reading;

namespace Heliograph.Pdf.Layout;

/// <summary>
/// The box round a paragraph with borders or shading, measured and drawn as the paragraph is placed. Joined to the paragraph
/// above with the same frame, it has no top border (the between border instead, if any) and starts where that one ended,
/// through the space between them; joined to the one below, it has no bottom border and carries on through its space after.
/// A paragraph broken across pages has a part of its box on each.
/// </summary>
internal sealed class ParagraphBox
{
    private readonly ParagraphFrame _frame;
    private readonly PageSet _pages;
    private readonly (double Left, double Right) _text;
    private readonly bool _joinedAbove;
    private double _top;
    private int _behind;
    private bool _first = true;

    private ParagraphBox(ParagraphFrame frame, PageSet pages, Paragraph p, bool joinedAbove, bool joinedBelow)
    {
        (_frame, _pages, _joinedAbove, JoinedBelow) = (frame, pages, joinedAbove, joinedBelow);
        var g = pages.Geometry;
        _text = (g.Left + p.Style.Indent.Left, g.Left + g.ContentWidth - p.Style.Indent.Right);
    }

    /// <summary>Whether the box carries on from the paragraph above.</summary>
    public bool JoinedAbove => _joinedAbove;

    /// <summary>Whether the box carries on into the paragraph below.</summary>
    public bool JoinedBelow { get; }

    /// <summary>
    /// The space the box takes above the text: its top border and that border's space, or, where it joins the paragraph
    /// above, the between border with its space on both sides, as LibreOffice lays it out.
    /// </summary>
    public double Above => _joinedAbove
        ? _frame.Between is { } between ? between.Width + (2 * between.Space) : 0
        : _frame.Top is { } top ? top.Width + top.Space : 0;

    /// <summary>The space the box takes below the text: its bottom border and that border's space, when it ends here.</summary>
    public double Below => !JoinedBelow && _frame.Bottom is { } b ? b.Width + b.Space : 0;

    /// <summary>The box for a paragraph, or null when it has neither borders nor shading.</summary>
    public static ParagraphBox? Of(Paragraph p, PageSet pages, ParagraphFrame? above, Block? next) =>
        p.Style.Frame is not { } frame ? null : new ParagraphBox(frame, pages, p, above == frame, next is Paragraph n && n.Style.Frame == frame);

    /// <summary>Starts a part of the box here, at the page's current position.</summary>
    public void Start()
    {
        _top = _pages.Y;
        _behind = _pages.Current.Ops.Count;
    }

    /// <summary>Draws the part of the box from where it started to the page's current position.</summary>
    public void End(bool last)
    {
        var (left, right) = (_text.Left - Space(_frame.Left), _text.Right + Space(_frame.Right));
        var (top, bottom, ops) = (_top, _pages.Y, _pages.Current.Ops);
        if (_frame.Shading is { } shading)
        {
            ops.Insert(_behind, new FillOp(left, top, right - left, bottom - top, shading));
        }

        // The top border at the box's top; the between border in the middle of its space.
        var over = _first ? (_joinedAbove ? _frame.Between : _frame.Top) : null;
        if (over is not null)
        {
            var y = top + (_joinedAbove ? over.Space : 0) + (over.Width / 2);
            ops.Add(new LineOp(left, y, right, y, over.Width));
        }

        if (last && !JoinedBelow && _frame.Bottom is { } under)
        {
            ops.Add(new LineOp(left, bottom - (under.Width / 2), right, bottom - (under.Width / 2), under.Width));
        }

        Side(_frame.Left, left, top, bottom);
        Side(_frame.Right, right, top, bottom);
        _first = false;
    }

    private static double Space(BorderLine? b) => b is null ? 0 : b.Space + (b.Width / 2);

    private void Side(BorderLine? b, double x, double top, double bottom)
    {
        if (b is not null)
        {
            _pages.Current.Ops.Add(new LineOp(x, top, x, bottom, b.Width));
        }
    }
}
