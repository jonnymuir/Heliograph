using Heliograph.Pdf.Fonts;
using Heliograph.Pdf.Reading;

namespace Heliograph.Pdf.Layout;

/// <summary>One line of a paragraph: its pieces at their offsets from the text area's left edge, and its height.</summary>
internal sealed class Line
{
    public List<(double X, Piece Piece)> Pieces { get; } = [];
    public double Limit { get; init; }
    public string Align { get; init; } = "left";
    public double Height { get; set; }
    public double Ascent { get; set; }

    /// <summary>Space under the text from line spacing over single, which may hang below the foot of a page.</summary>
    public double Hang { get; set; }
    public bool Last { get; set; }
    public bool EndsWithBreak { get; set; }
}

/// <summary>Breaks a paragraph into lines the way Word does: greedily, at spaces, with tab stops and hanging labels.</summary>
internal static class LineBreaker
{

    public static List<Line> Break(Paragraph p, double width, Func<string, string> field)
    {
        var builder = new LineBuilder(p.Style, width - p.Style.Indent.Right);
        if (p.Label is { } label)
        {
            builder.Label(new Piece(label.Text, label.Style, TextLook.Width(label.Text, label.Style), PieceKind.Word, IsLabel: true), p.Placement);
        }

        foreach (var piece in Pieces(p, field))
        {
            builder.Add(piece);
        }

        var lines = builder.Lines;
        lines[^1].Last = true;
        foreach (var line in lines)
        {
            Measure(line, p);
        }

        return lines;
    }

    /// <summary>The lines of one paragraph so far, the one being filled, and how far along it the next piece goes.</summary>
    private sealed class LineBuilder
    {
        private readonly ParagraphStyle _style;
        private readonly double _limit;
        private Line _line;
        private double _x;
        private bool _tabbed;
        private bool _afterTab;

        public LineBuilder(ParagraphStyle style, double limit)
        {
            (_style, _limit) = (style, limit);
            _line = NewLine();
            _x = style.Indent.Left + style.Indent.FirstLine;
        }

        public List<Line> Lines { get; } = [];

        // A label sits where its placement says, then a tab, a space or nothing comes before the text.
        public void Label(Piece label, LabelPlacement placement)
        {
            var start = LabelLayout.Start(placement, _x, label.Width);
            _line.Pieces.Add((start, label));
            var end = start + label.Width;
            _x = placement.Suffix == "tab" ? NextTab(end, _style, hanging: true) : end + LabelLayout.Gap(placement, label.Style);
        }

        public void Add(Piece piece)
        {
            var tabbed = _tabbed;
            _tabbed = piece.Kind == PieceKind.Tab;
            switch (piece.Kind)
            {
                case PieceKind.Break:
                    _line.EndsWithBreak = true;
                    Next();
                    break;
                case PieceKind.Tab:
                    // A tab whose stop is past the line's end starts the next line, and still takes effect there.
                    var stop = NextTab(_x, _style, hanging: false);
                    if (stop > _line.Limit)
                    {
                        Next();
                        stop = NextTab(_x, _style, hanging: false);
                    }

                    _x = stop;
                    break;
                case PieceKind.Word when _x + piece.Width > _line.Limit + 0.01:
                    _afterTab = tabbed;
                    Wrap(piece);
                    break;
                default:
                    Put(piece);
                    break;
            }
        }

        // A word that does not fit starts the next line, and a tab just before it goes with it and takes effect there. A
        // word too wide for any line is split where the line is full; a picture never is, and goes on a line of its own.
        private void Wrap(Piece piece)
        {
            if (_line.Pieces.Any(q => q.Piece.Kind == PieceKind.Word && !q.Piece.IsLabel))
            {
                Next();
                _x = _afterTab ? NextTab(_x, _style, hanging: false) : _x;
            }

            if (piece.Picture is not null)
            {
                Put(piece);
                return;
            }

            var text = piece.Text;
            while (_x + TextLook.Width(text, piece.Style) > _line.Limit + 0.01 && text.Length > 1)
            {
                var fit = Fit(text, piece.Style);
                Put(piece with { Text = text[..fit], Width = TextLook.Width(text[..fit], piece.Style) });
                Next();
                text = text[fit..];
            }

            Put(piece with { Text = text, Width = TextLook.Width(text, piece.Style) });
        }

        // How many characters of a word fit on the rest of the line: at least one.
        private int Fit(string text, RunStyle style)
        {
            var fit = 1;
            while (fit < text.Length && _x + TextLook.Width(text[..(fit + 1)], style) <= _line.Limit + 0.01)
            {
                fit++;
            }

            return fit;
        }

        private void Put(Piece piece)
        {
            _line.Pieces.Add((_x, piece));
            _x += piece.Width;
        }

        private void Next()
        {
            _line = NewLine();
            _x = _style.Indent.Left;
        }

        private Line NewLine()
        {
            var line = new Line { Limit = _limit, Align = _style.Align };
            Lines.Add(line);
            return line;
        }
    }

    private static List<Piece> Pieces(Paragraph p, Func<string, string> field)
    {
        var pieces = new List<Piece>();
        foreach (var inline in p.Inlines)
        {
            switch (inline)
            {
                case TabInline:
                    pieces.Add(new Piece("\t", inline.Style, 0, PieceKind.Tab));
                    break;
                case BreakInline:
                    pieces.Add(new Piece("\n", inline.Style, 0, PieceKind.Break));
                    break;
                case TextInline t:
                    Words(t.Text, t.Style, pieces);
                    break;
                case FieldInline f:
                    Words(field(f.Field), f.Style, pieces);
                    break;
                case PictureInline picture:
                    pieces.Add(new Piece("", picture.Style, picture.Width, PieceKind.Word) { Picture = picture });
                    break;
            }
        }

        return pieces;
    }

    private static void Words(string text, RunStyle style, List<Piece> pieces)
    {
        for (var i = 0; i < text.Length;)
        {
            var space = text[i] == ' ';
            var j = i;
            while (j < text.Length && (text[j] == ' ') == space)
            {
                j++;
            }

            foreach (var chunk in space ? [text[i..j]] : BreakableParts(text[i..j]))
            {
                pieces.Add(new Piece(chunk, style, TextLook.Width(chunk, style), space ? PieceKind.Space : PieceKind.Word));
            }

            i = j;
        }
    }

    // A line may also break after a hyphen or dash between letters, as in "well-" and "known", as Word and LibreOffice do.
    private static List<string> BreakableParts(string word)
    {
        var parts = new List<string>();
        var start = 0;
        for (var k = 1; k < word.Length - 1; k++)
        {
            if (IsBreakingHyphen(word, k))
            {
                parts.Add(word[start..(k + 1)]);
                start = k + 1;
            }
        }

        parts.Add(word[start..]);
        return parts;
    }

    private static bool IsBreakingHyphen(string word, int k) =>
        word[k] is '-' or '\u2010' or '\u2013' && char.IsLetter(word[k - 1]) && char.IsLetter(word[k + 1]);

    // Tab stops are measured from the text area's left edge. A hanging indent is a stop for the label's tab.
    private static double NextTab(double x, ParagraphStyle s, bool hanging)
    {
        var stops = hanging && s.Indent.FirstLine < 0 ? s.Tabs.Append(s.Indent.Left) : s.Tabs;
        foreach (var stop in stops.Order())
        {
            if (stop > x + 0.01)
            {
                return stop;
            }
        }

        return (Math.Floor((x + 0.01) / s.DefaultTab) + 1) * s.DefaultTab;
    }

    private static void Measure(Line line, Paragraph p)
    {
        var styles = line.Pieces.Select(q => q.Piece.Style).DefaultIfEmpty(p.MarkStyle).ToList();
        // The tallest reach above the baseline, the deepest below it, and the largest gap, each from whichever font has it.
        // A picture stands on the baseline, so it reaches as far above it as it is tall.
        // Line spacing over or under single adds or takes away a share of the text's height, not the picture's.
        var metrics = styles.Select(FontLibrary.Metrics).ToList();
        var (ascent, gap) = (metrics.Max(m => m.Ascent), metrics.Max(m => m.Gap));
        var below = metrics.Max(m => m.Descent) + gap;
        line.Ascent = Math.Max(ascent, line.Pieces.Select(q => q.Piece.Picture?.Height ?? 0).DefaultIfEmpty(0).Max());
        var natural = line.Ascent + below;
        line.Height = LineHeights.Height(p.Style.Spacing, natural, ascent + below);
        line.Hang = LineHeights.Hang(p.Style.Spacing, line.Height, natural);
        line.Ascent += LineHeights.Lift(p.Style.Spacing, line.Height, natural, gap);
    }
}
