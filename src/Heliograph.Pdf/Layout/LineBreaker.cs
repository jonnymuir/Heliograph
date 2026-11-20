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

    /// <summary>What a block set in a cell like a line draws, a table inside a table, from the line's top left; else null.</summary>
    public IReadOnlyList<Op>? Block { get; init; }

    /// <summary>Whether the line ends with a page break, so the paragraph goes on at the top of the next page.</summary>
    public bool PageBreakAfter { get; set; }

    /// <summary>Whether that break is a column break, going on in the next column rather than on the next page.</summary>
    public bool ColumnBreakAfter { get; set; }
}

/// <summary>Where a paragraph starts down its page, and the room a line has across it at a height, beside floating pictures.</summary>
internal sealed record LineBounds(double Top, Func<double, double, LineRoom> Room);

/// <summary>Breaks a paragraph into lines the way Word does: greedily, at spaces, with tab stops and hanging labels.</summary>
internal static class LineBreaker
{
    public static List<Line> Break(Paragraph p, double width, Func<FieldInline, string> field) => Break(p, width, field, null);

    /// <summary>
    /// Breaks a paragraph into lines, each as wide as the room the floating pictures on its page leave it, from where the
    /// paragraph starts down.
    /// </summary>
    public static List<Line> Break(Paragraph p, double width, Func<FieldInline, string> field, LineBounds? bounds)
    {
        var builder = new LineBuilder(p, width - p.Style.Indent.Right, bounds);
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

        // A page break that ends its paragraph keeps the paragraph mark on its own line, as Word and LibreOffice lay it out:
        // the paragraph ends there, and what follows starts on the next page.
        if (lines is [.., { PageBreakAfter: true }, { Pieces.Count: 0 }])
        {
            lines.RemoveAt(lines.Count - 1);
        }

        lines[^1].Last = true;
        return lines;
    }

    /// <summary>The lines of one paragraph so far, the one being filled, and how far along it the next piece goes.</summary>
    private sealed class LineBuilder
    {
        private readonly Paragraph _p;
        private readonly ParagraphStyle _style;
        private readonly double _limit;
        private readonly LineBounds? _bounds;
        private double _y;
        private double _height;
        private Line _line;
        private double _x;
        private bool _tabbed;
        private bool _afterTab;
        private (int Index, double Origin, TabStop Stop, RunStyle Style)? _segment;

        public LineBuilder(Paragraph p, double limit, LineBounds? bounds)
        {
            (_p, _style, _limit, _bounds) = (p, p.Style, limit, bounds);
            (_y, _height) = (bounds?.Top ?? 0, Natural(p));
            _x = _style.Indent.Left + _style.Indent.FirstLine;
            _line = NewLine();
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
                    _line.PageBreakAfter = piece.PageBreak;
                    _line.ColumnBreakAfter = piece.ColumnBreak;
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

            if (piece.Picture is not null || piece.Segments is not null)
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
            Measure(_line, _p);
            (_y, _height) = (_y + _line.Height, _line.Height);
            _x = _style.Indent.Left;
            _line = NewLine();
        }

        /// <summary>Lines up the text after the last tab, and measures the last line, once the paragraph ends.</summary>
        public void Finish()
        {
            CloseSegment();
            Measure(_line, _p);
        }

        // A tab whose stop is past the line's end starts the next line, and still takes effect there. Text after a left
        // stop starts on it; text after a right, centred or decimal stop is lined up on it when it ends.
        private void Tab(Piece tab)
        {
            CloseSegment();
            if (tab.Position is { } position)
            {
                Start(Absolute(position), tab.Style);
                return;
            }

            var stop = TabStops.Next(_x, _style, hanging: false);
            if (TabStops.PastEnd(stop, _line.Limit))
            {
                Next();
                stop = TabStops.Next(_x, _style, hanging: false);
            }

            Start(stop, tab.Style);
        }

        // A position tab's stop: the left, the middle or the right of the margins or of the paragraph's indents.
        private TabStop Absolute(PositionTab tab)
        {
            var (left, right) = tab.RelativeTo == "indent" ? (_style.Indent.Left, _limit) : (0, _limit + _style.Indent.Right);
            var at = tab.Alignment switch { "center" => (left + right) / 2, "right" => right, _ => left };
            return new TabStop(at, tab.Alignment, tab.Leader switch { "dot" => "dot", "hyphen" => "hyphen", "underscore" => "underscore", "middleDot" => "middleDot", _ => "none" });
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

        // A new line, as wide as the room beside the floating pictures at its height; where there is none, below them, an
        // empty line keeping the space between.
        private Line NewLine()
        {
            var room = Room();
            while (room.Skip > 0)
            {
                Lines.Add(new Line { Limit = _limit, Align = _style.Align, Height = room.Skip });
                _y += room.Skip;
                room = Room();
            }

            _x = Math.Max(_x, room.Left);
            var line = new Line { Limit = Math.Min(_limit, room.Right), Align = _style.Align };
            Lines.Add(line);
            return line;
        }

        private LineRoom Room() => _bounds is { } b ? b.Room(_y, _height) : new LineRoom(0, _limit, 0);
    }

    // The height of an empty line of the paragraph, taken for its first line's before it is filled.
    private static double Natural(Paragraph p)
    {
        var probe = new Line();
        Measure(probe, p);
        return probe.Height;
    }

    private static List<Piece> Pieces(Paragraph p, Func<FieldInline, string> field)
    {
        var pieces = new List<Piece>();
        foreach (var inline in p.Inlines)
        {
            switch (inline)
            {
                case TabInline t:
                    pieces.Add(new Piece("\t", inline.Style, 0, PieceKind.Tab) { Position = t.Position });
                    break;
                case BreakInline b:
                    pieces.Add(new Piece("\n", inline.Style, 0, PieceKind.Break) { PageBreak = b.Page, ColumnBreak = b.Column });
                    break;
                case TextInline t:
                    Words(t.Text, t.Style, pieces);
                    break;
                case FieldInline f:
                    Words(field(f), f.Style, pieces);
                    break;
                case NoteInline n:
                    pieces.Add(n.End ? Segments.Piece(n.Mark, n.Style, PieceKind.Word) with { Endnote = n.Id } : Segments.Piece(n.Mark, n.Style, PieceKind.Word) with { Note = n.Id });
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
                pieces.Add(Segments.Piece(chunk, style, space ? PieceKind.Space : PieceKind.Word));
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
        line.Ascent += LineHeights.Lift(p.Style.Spacing, line, natural, gap);
    }
}
