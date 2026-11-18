using Heliograph.Pdf.Reading;

namespace Heliograph.Pdf.Layout;

/// <summary>
/// A page's footnotes, as LibreOffice lays out a Word document: at the foot of the text area, under a line of the default
/// style's height with the separator, two inches long, drawn across it; the body's text stops above. A line that refers
/// to a note goes on a page only with its note. A note is not split across pages.
/// </summary>
internal sealed class Footnotes
{
    private const double SeparatorLength = 144;
    private const double SeparatorWidth = 0.5;

    private readonly Dictionary<string, List<(Paragraph P, List<Line> Lines)>> _notes = new(StringComparer.Ordinal);
    private readonly Dictionary<string, double> _heights = new(StringComparer.Ordinal);
    private readonly List<(string Id, StructNode? Owner)> _page = [];
    private readonly PageSet _pages;
    private readonly double _gap;

    public Footnotes(WordDocument document, PageSet pages)
    {
        _pages = pages;
        var width = pages.Geometry.ContentWidth;
        foreach (var (id, blocks) in document.Notes)
        {
            var paragraphs = blocks.OfType<Paragraph>().Select(p => (p, LineBreaker.Break(p, width, _ => ""))).ToList();
            _notes[id] = paragraphs;
            _heights[id] = paragraphs.Sum(n => n.p.Style.SpaceAbove + n.Item2.Sum(l => l.Height) + n.p.Style.Spacing.After);
        }

        _gap = document.NoteSeparator is { } separator ? LineBreaker.Break(separator, width, _ => "").Sum(l => l.Height) : 0;
        pages.Finishing = Draw;
    }

    /// <summary>The room these lines' notes would take on this page, with the line above them if they would be its first.</summary>
    public double Cost(IEnumerable<Line> lines)
    {
        var height = lines.SelectMany(In).Sum(Height);
        return height > 0 && _page.Count == 0 ? height + _gap : height;
    }

    /// <summary>Puts a line's notes on this page, belonging to the line's paragraph.</summary>
    public void Place(Line line, StructNode? owner)
    {
        foreach (var id in In(line))
        {
            _pages.Reserved += Height(id) + (_page.Count == 0 ? _gap : 0);
            _page.Add((id, owner));
        }
    }

    private static IEnumerable<string> In(Line line) => line.Pieces.Select(p => p.Piece.Note).OfType<string>();

    private double Height(string id) => _heights.GetValueOrDefault(id);

    // The page's notes at the foot of its text area, and the separator above them; each note tagged as a note in its paragraph.
    private void Draw(LaidOutPage page, double bottom)
    {
        if (_page.Count == 0)
        {
            return;
        }

        var g = _pages.Geometry;
        var y = bottom - _page.Sum(n => Height(n.Id));
        var line = y - (_gap * 0.3925);
        page.Ops.Add(new LineOp(g.Left, line, g.Left + Math.Min(SeparatorLength, g.ContentWidth), line, SeparatorWidth));
        foreach (var (id, owner) in _page)
        {
            var node = owner is null ? null : new StructNode("Note", owner) { Id = "note-" + string.Concat(id.Where(char.IsAsciiLetterOrDigit)) };
            owner?.Kids.Add(node!);
            foreach (var (p, lines) in _notes.GetValueOrDefault(id) ?? [])
            {
                y += p.Style.SpaceAbove;
                foreach (var l in lines)
                {
                    LinePlacer.Place(page, l, g.Left, y, node is null ? Owner.Artifact : new Owner(node, null));
                    y += l.Height;
                }

                y += p.Style.Spacing.After;
            }
        }

        _page.Clear();
    }
}
