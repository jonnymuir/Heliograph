using Heliograph.Pdf.Fonts;
using Heliograph.Pdf.Reading;

namespace Heliograph.Pdf.Layout;

internal enum PieceKind
{
    Word,
    Space,
    Tab,
    Break,
}

/// <summary>A word, a run of spaces, a tab or a line break, measured in its style.</summary>
internal sealed record Piece(string Text, RunStyle Style, double Width, PieceKind Kind, bool IsLabel = false);

/// <summary>One line of a paragraph: its pieces at their offsets from the text area's left edge, and its height.</summary>
internal sealed class Line
{
    public List<(double X, Piece Piece)> Pieces { get; } = [];
    public double Limit { get; init; }
    public string Align { get; init; } = "left";
    public double Height { get; set; }
    public double Ascent { get; set; }
    public bool Last { get; set; }
    public bool EndsWithBreak { get; set; }
}

/// <summary>Breaks a paragraph into lines the way Word does: greedily, at spaces, with tab stops and hanging labels.</summary>
internal static class LineBreaker
{
    private const double DefaultTab = 36;

    public static List<Line> Break(Paragraph p, double width, Func<string, string> field)
    {
        var s = p.Style;
        var lines = new List<Line>();
        var limit = width - s.Indent.Right;
        Line Next()
        {
            var line = new Line { Limit = limit, Align = s.Align };
            lines.Add(line);
            return line;
        }

        var current = Next();
        var x = s.Indent.Left + s.Indent.FirstLine;
        if (p.Label is { } label)
        {
            var piece = new Piece(label.Text, label.Style, FontLibrary.Face(label.Style).Width(label.Text, label.Style.Size), PieceKind.Word, IsLabel: true);
            current.Pieces.Add((x, piece));
            x = NextTab(x + piece.Width, s, hanging: true);
        }

        foreach (var piece in Pieces(p, field))
        {
            (current, x) = Add(piece, current, x, s, Next);
        }

        lines[^1].Last = true;
        foreach (var line in lines)
        {
            Measure(line, p);
        }

        return lines;
    }

    private static (Line Line, double X) Add(Piece piece, Line line, double x, ParagraphStyle s, Func<Line> next)
    {
        switch (piece.Kind)
        {
            case PieceKind.Break:
                line.EndsWithBreak = true;
                return (next(), s.Indent.Left);
            case PieceKind.Tab:
                var stop = NextTab(x, s, hanging: false);
                return stop > line.Limit ? (next(), s.Indent.Left) : (line, stop);
            case PieceKind.Word when x + piece.Width > line.Limit + 0.01 && line.Pieces.Any(q => q.Piece.Kind == PieceKind.Word && !q.Piece.IsLabel):
                var wrapped = next();
                wrapped.Pieces.Add((s.Indent.Left, piece));
                return (wrapped, s.Indent.Left + piece.Width);
            default:
                line.Pieces.Add((x, piece));
                return (line, x + piece.Width);
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
            }
        }

        return pieces;
    }

    private static void Words(string text, RunStyle style, List<Piece> pieces)
    {
        var face = FontLibrary.Face(style);
        for (var i = 0; i < text.Length;)
        {
            var space = text[i] == ' ';
            var j = i;
            while (j < text.Length && (text[j] == ' ') == space)
            {
                j++;
            }

            var chunk = text[i..j];
            pieces.Add(new Piece(chunk, style, face.Width(chunk, style.Size), space ? PieceKind.Space : PieceKind.Word));
            i = j;
        }
    }

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

        return (Math.Floor(x / DefaultTab) + 1) * DefaultTab;
    }

    private static void Measure(Line line, Paragraph p)
    {
        var styles = line.Pieces.Select(q => q.Piece.Style).DefaultIfEmpty(p.MarkStyle).ToList();
        var natural = styles.Max(st => FontLibrary.Face(st).LineHeight(st.Size));
        line.Ascent = styles.Max(st => FontLibrary.Face(st).Ascent(st.Size));
        var spacing = p.Style.Spacing;
        line.Height = spacing.LineRule switch
        {
            "exact" => spacing.Line / 20,
            "atLeast" => Math.Max(spacing.Line / 20, natural),
            _ => natural * spacing.Line / 240,
        };
    }
}
