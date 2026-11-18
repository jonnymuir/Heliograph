using Heliograph.Pdf.Reading;

namespace Heliograph.Pdf.Layout;

/// <summary>
/// The letter's endnotes, as Word and LibreOffice lay them out: after its text, one single line of the default style
/// below the foot of its last line with the separator, two inches long, drawn across that line, and then each note in the
/// order first referred to, carrying on to further pages as they need. Each is tagged as a note in the paragraph that
/// refers to it.
/// </summary>
internal sealed class Endnotes(WordDocument document, PageSet pages, StructNode root)
{
    private readonly Dictionary<string, StructNode?> _owners = new(StringComparer.Ordinal);

    /// <summary>Notes which paragraph refers to each of a line's endnotes.</summary>
    public void Place(Line line, StructNode? owner)
    {
        foreach (var id in line.Pieces.Select(p => p.Piece.Endnote).OfType<string>())
        {
            _owners.TryAdd(id, owner);
        }
    }

    /// <summary>Lays the endnotes out after the text laid out last.</summary>
    public void Run()
    {
        if (document.Endnotes.Count == 0)
        {
            return;
        }

        var g = pages.Geometry;
        var gap = document.NoteSeparator is { } separator ? LineBreaker.Break(separator, g.ContentWidth, _ => "").Sum(l => l.Height) : 0;
        pages.Y = pages.TextFoot;
        var line = pages.Y + (gap * 0.6075);
        pages.Current.Ops.Add(new LineOp(g.Left, line, g.Left + Math.Min(144, g.ContentWidth), line, 0.5));
        pages.Y += gap;
        foreach (var (id, blocks) in document.Endnotes)
        {
            var owner = _owners.GetValueOrDefault(id) ?? root;
            var node = new StructNode("Note", owner) { Id = "endnote-" + string.Concat(id.Where(char.IsAsciiLetterOrDigit)) };
            owner.Kids.Add(node);
            foreach (var p in blocks.OfType<Paragraph>())
            {
                Paragraph(p, node);
            }
        }
    }

    // A note's paragraph, a line at a time, each going to a new page when it does not fit.
    private void Paragraph(Paragraph p, StructNode node)
    {
        var g = pages.Geometry;
        pages.Y += p.Style.SpaceAbove;
        foreach (var l in LineBreaker.Break(p, g.ContentWidth, _ => ""))
        {
            if (pages.Y + l.Height - l.Hang > pages.BodyBottom + 0.01 && pages.HasContent)
            {
                pages.NewPage();
            }

            LinePlacer.Place(pages.Current, l, g.Left, pages.Y, new Owner(node, null));
            pages.Y += l.Height;
            pages.HasContent = true;
        }

        pages.Y = Math.Min(pages.Y + p.Style.Spacing.After, pages.BodyBottom);
    }
}
