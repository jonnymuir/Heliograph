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

    private readonly IReadOnlyDictionary<string, IReadOnlyList<Block>> _blocks;
    private readonly Dictionary<(string Id, double Width), List<(Paragraph P, List<Line> Lines)>> _laidOut = [];
    private readonly List<(string Id, StructNode? Owner)> _page = [];
    private readonly PageSet _pages;
    private readonly Paragraph? _separator;

    public Footnotes(WordDocument document, PageSet pages)
    {
        (_blocks, _pages, _separator) = (document.Notes, pages, document.NoteSeparator);
        pages.Finishing = Draw;
    }

    // The line above the notes: one single line of the default style.
    private double Gap => _separator is null ? 0 : LineBreaker.Break(_separator, _pages.Geometry.ContentWidth, _ => "").Sum(l => l.Height);

    /// <summary>The room these lines' notes would take on this page, with the line above them if they would be its first.</summary>
    public double Cost(IEnumerable<Line> lines)
    {
        var height = lines.SelectMany(In).Sum(Height);
        return height > 0 && _page.Count == 0 ? height + Gap : height;
    }

    /// <summary>Puts a line's notes on this page, belonging to the line's paragraph.</summary>
    public void Place(Line line, StructNode? owner)
    {
        foreach (var id in In(line))
        {
            _pages.Reserved += Height(id) + (_page.Count == 0 ? Gap : 0);
            _page.Add((id, owner));
        }
    }

    private static IEnumerable<string> In(Line line) => line.Pieces.Select(p => p.Piece.Note).OfType<string>();

    private double Height(string id) => Lines(id).Sum(n => n.P.Style.SpaceAbove + n.Lines.Sum(l => l.Height) + n.P.Style.Spacing.After);

    // A note's paragraphs broken into lines across this page's text, once for each width a section gives it.
    private List<(Paragraph P, List<Line> Lines)> Lines(string id)
    {
        var width = _pages.Geometry.ContentWidth;
        if (!_laidOut.TryGetValue((id, width), out var lines))
        {
            lines = (_blocks.GetValueOrDefault(id) ?? []).OfType<Paragraph>().Select(p => (p, LineBreaker.Break(p, width, _ => ""))).ToList();
            _laidOut[(id, width)] = lines;
        }

        return lines;
    }

    // The page's notes at the foot of its text area, and the separator above them; each note tagged as a note in its paragraph.
    private void Draw(LaidOutPage page, double bottom)
    {
        if (_page.Count == 0)
        {
            return;
        }

        var g = page.Section.Page;
        var y = bottom - _page.Sum(n => Height(n.Id));
        var line = y - (Gap * 0.3925);
        page.Ops.Add(new LineOp(g.Left, line, g.Left + Math.Min(SeparatorLength, g.ContentWidth), line, SeparatorWidth));
        foreach (var (id, owner) in _page)
        {
            var node = owner is null ? null : new StructNode("Note", owner) { Id = "note-" + string.Concat(id.Where(char.IsAsciiLetterOrDigit)) };
            owner?.Kids.Add(node!);
            foreach (var (p, lines) in Lines(id))
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
