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

        builder.Finish();
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
        private (int Index, double Origin, TabStop Stop, RunStyle Style)? _segment;

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
            _x = placement.Suffix == "tab" ? TabStops.Next(end, _style, hanging: true).Position : end + LabelLayout.Gap(placement, label.Style);
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
                    Tab(piece);
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
                if (_afterTab)
                {
                    Start(TabStops.Next(_x, _style, hanging: false), piece.Style);
                }
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
            CloseSegment();
            _line = NewLine();
            _x = _style.Indent.Left;
        }

        /// <summary>Lines up the text after the last tab, once the paragraph ends.</summary>
        public void Finish() => CloseSegment();

        // A tab whose stop is past the line's end starts the next line, and still takes effect there. Text after a left
        // stop starts on it; text after a right, centred or decimal stop is lined up on it when it ends.
        private void Tab(Piece tab)
        {
            CloseSegment();
            var stop = TabStops.Next(_x, _style, hanging: false);
            if (TabStops.PastEnd(stop, _line.Limit))
            {
                Next();
                stop = TabStops.Next(_x, _style, hanging: false);
            }

            Start(stop, tab.Style);
        }

        // Text after a left stop starts on it; after a right, centred or decimal stop it is gathered, to line up later.
        private void Start(TabStop stop, RunStyle style)
        {
            if (stop.Align == "left")
            {
                AddLeader(stop, _x, stop.Position, style, _line.Pieces.Count);
                _x = stop.Position;
                return;
            }

            _segment = (_line.Pieces.Count, _x, stop, style);
        }

        // The text since a right, centred or decimal tab, moved onto its stop, with the leader before it.
        private void CloseSegment()
        {
            if (_segment is not var (index, origin, stop, style))
            {
                return;
            }

            _segment = null;
            var pieces = _line.Pieces.GetRange(index, _line.Pieces.Count - index);
            var start = TabStops.Start(stop, origin, _x - origin, ToDecimal(pieces, origin));
            for (var k = index; k < _line.Pieces.Count; k++)
            {
                _line.Pieces[k] = (_line.Pieces[k].X + start - origin, _line.Pieces[k].Piece);
            }

            _x += start - origin;
            AddLeader(stop, origin, start, style, index);
        }

        // How far along text after a tab its first full stop comes; all of it when it has none.
        private static double ToDecimal(List<(double X, Piece Piece)> pieces, double origin)
        {
            foreach (var (x, piece) in pieces)
            {
                if (piece.Text.IndexOf('.', StringComparison.Ordinal) is var dot and >= 0)
                {
                    return x - origin + TextLook.Width(piece.Text[..dot], piece.Style);
                }
            }

            return pieces.Count == 0 ? 0 : pieces[^1].X + pieces[^1].Piece.Width - origin;
        }

        private void AddLeader(TabStop stop, double from, double to, RunStyle style, int at)
        {
            // A leader ends where the text starts.
            if (TabStops.Leader(stop, from, to, style) is { } leader)
            {
                _line.Pieces.Insert(at, (to - leader.Width, leader));
            }
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
