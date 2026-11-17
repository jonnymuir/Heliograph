namespace Heliograph.PdfExperiment.OwnEngine;

// Lays a WordDocument out on pages: lines broken greedily as Word does, pages broken with widow, orphan, keep-with-next
// and keep-together rules, tables row by row with header rows repeated, and headers and footers on every page. It also
// builds the structure a screen reader follows (headings, paragraphs, lists, tables); headers and footers are artifacts.

public sealed class StructNode(string type, StructNode? parent)
{
    public string Type { get; } = type;
    public StructNode? Parent { get; } = parent;
    public List<object> Kids { get; } = [];
    public string? Scope { get; init; }
    public int ColSpan { get; init; } = 1;

    public StructNode Add(string type, string? scope = null, int colSpan = 1)
    {
        var child = new StructNode(type, this) { Scope = scope, ColSpan = colSpan };
        Kids.Add(child);
        return child;
    }
}

public sealed record MarkedRef(int Page, int Mcid);

public abstract record Op;
public sealed record TextOp(double X, double Baseline, string Text, RunStyle Style, StructNode? Node) : Op;
public sealed record LineOp(double X1, double Y1, double X2, double Y2, double Width) : Op;

public sealed class LaidOutPage
{
    public List<Op> Ops { get; } = [];
}

public sealed class Fonts(string folder)
{
    private readonly Dictionary<(bool, bool), TrueType> _faces = [];

    public TrueType Face(RunStyle style)
    {
        var key = (style.Bold, style.Italic);
        if (!_faces.TryGetValue(key, out var face))
        {
            var file = (style.Bold, style.Italic) switch
            {
                (true, true) => "LiberationSans-BoldItalic.ttf",
                (true, false) => "LiberationSans-Bold.ttf",
                (false, true) => "LiberationSans-Italic.ttf",
                _ => "LiberationSans-Regular.ttf",
            };
            _faces[key] = face = new TrueType(File.ReadAllBytes(Path.Combine(folder, file)));
        }

        return face;
    }
}

public sealed class Layout(Fonts fonts)
{
    private const double DefaultTab = 36;
    private Section _section = null!;
    private List<LaidOutPage> _pages = [];
    private LaidOutPage _page = null!;
    private double _y;
    private double _bodyTop;
    private double _bodyBottom;
    private bool _pageHasContent;

    public StructNode Root { get; } = new("Document", null);

    public IReadOnlyList<LaidOutPage> Run(WordDocument document)
    {
        _section = document.Section;
        _pages = [];
        var header = Math.Max(Height(_section.Header), Height(_section.FirstHeader));
        var footer = Math.Max(Height(_section.Footer), Height(_section.FirstFooter));
        _bodyTop = Math.Max(_section.Top, _section.HeaderDistance + header);
        _bodyBottom = _section.Height - Math.Max(_section.Bottom, _section.FooterDistance + footer);
        NewPage();

        StructNode? list = null;
        var blocks = document.Body;
        for (var i = 0; i < blocks.Count; i++)
        {
            if (blocks[i] is Paragraph p)
            {
                StructNode node;
                if (p.IsListItem)
                {
                    list ??= Root.Add("L");
                    var item = list.Add("LI");
                    var label = item.Add("Lbl");
                    node = item.Add("LBody");
                    BodyParagraph(p, node, label, i + 1 < blocks.Count ? blocks[i + 1] : null);
                }
                else
                {
                    list = null;
                    node = Root.Add(p.Style.OutlineLevel is { } level && level < 6 ? $"H{level + 1}" : "P");
                    BodyParagraph(p, node, null, i + 1 < blocks.Count ? blocks[i + 1] : null);
                }
            }
            else if (blocks[i] is Table t)
            {
                list = null;
                BodyTable(t, Root.Add("Table"));
            }
        }

        for (var n = 0; n < _pages.Count; n++) Decorate(n, _pages.Count);
        return _pages;
    }

    private void NewPage()
    {
        _page = new LaidOutPage();
        _pages.Add(_page);
        _y = _bodyTop;
        _pageHasContent = false;
    }

    private double ContentLeft => _section.Left;
    private double ContentWidth => _section.Width - _section.Left - _section.Right;

    // ---- Paragraphs in the body, broken across pages ----

    private void BodyParagraph(Paragraph p, StructNode node, StructNode? labelNode, Block? next)
    {
        var s = p.Style;
        if (s.PageBreakBefore && _pageHasContent) NewPage();
        var lines = Break(p, ContentWidth, _ => "");
        var before = _pageHasContent || _pages.Count == 1 ? s.Before : 0;
        var total = before + lines.Sum(l => l.Height);

        // Keep with next: this paragraph and the first line of the next block share a page.
        var keep = s.KeepNext && next is not null ? FirstLineHeight(next) : 0;
        if ((s.KeepLines || s.KeepNext) && _pageHasContent && _y + total + keep > _bodyBottom && total + keep <= _bodyBottom - _bodyTop)
        {
            NewPage();
            before = 0;
        }

        _y += before;
        var placed = 0;
        while (placed < lines.Count)
        {
            var fit = 0;
            var y = _y;
            while (placed + fit < lines.Count && y + lines[placed + fit].Height <= _bodyBottom + 0.01) y += lines[placed + fit++].Height;
            var remaining = lines.Count - placed;
            if (fit < remaining && s.WidowControl)
            {
                if (fit == 1 && placed == 0 && remaining > 1) fit = 0;          // no orphan: a lone first line at the foot
                else if (remaining - fit == 1 && fit >= 2) fit--;               // no widow: a lone last line at the head
            }

            if (fit == 0 && !_pageHasContent) fit = 1;                          // a line taller than the page still goes somewhere
            for (var k = 0; k < fit; k++)
            {
                Place(lines[placed + k], ContentLeft, _y, node, labelNode);
                _y += lines[placed + k].Height;
                _pageHasContent = true;
            }

            placed += fit;
            if (placed < lines.Count) NewPage();
        }

        _y += s.After;
        if (_y > _bodyBottom) _y = _bodyBottom;
    }

    private double FirstLineHeight(Block block) => block switch
    {
        Paragraph p => p.Style.Before + (Break(p, ContentWidth, _ => "").FirstOrDefault()?.Height ?? 0),
        Table t when t.Rows.Count > 0 => RowHeight(t, t.Rows[0]),
        _ => 0,
    };

    // ---- Tables, row by row, header rows repeated on each new page ----

    private void BodyTable(Table t, StructNode node)
    {
        var headers = t.Rows.TakeWhile(r => r.Header).ToList();
        var first = true;
        foreach (var row in t.Rows)
        {
            var height = RowHeight(t, row);
            if (_y + height > _bodyBottom && _pageHasContent)
            {
                NewPage();
                if (!row.Header)
                    foreach (var h in headers) DrawRow(t, h, null);
            }

            DrawRow(t, row, node.Add("TR"));
            first = false;
        }

        _ = first;
    }

    private double RowHeight(Table t, Row row)
    {
        var height = 0.0;
        var column = 0;
        foreach (var cell in row.Cells)
        {
            var width = TextWidth(t, cell, t.Grid.Skip(column).Take(cell.Span).Sum());
            column += cell.Span;
            var h = t.MarginTop + t.MarginBottom + (cell.Top?.Width ?? 0);
            foreach (var b in cell.Blocks.OfType<Paragraph>())
                h += b.Style.Before + Break(b, width, _ => "").Sum(l => l.Height) + b.Style.After;
            height = Math.Max(height, h);
        }

        return height;
    }

    // Word and LibreOffice place a table's text at the indent, so its border sits one cell padding to the left.
    private static double TextWidth(Table t, Cell cell, double width) =>
        width - t.MarginLeft - t.MarginRight - ((cell.Left?.Width ?? 0) + (cell.Right?.Width ?? 0)) / 2;

    private void DrawRow(Table t, Row row, StructNode? rowNode)
    {
        var height = RowHeight(t, row);
        var x = ContentLeft + t.Indent - t.MarginLeft;
        var column = 0;
        foreach (var cell in row.Cells)
        {
            var width = t.Grid.Skip(column).Take(cell.Span).Sum();
            column += cell.Span;
            var cellNode = rowNode?.Add(row.Header ? "TH" : "TD", row.Header ? "Column" : null, cell.Span);
            var y = _y + t.MarginTop + (cell.Top?.Width ?? 0);
            foreach (var b in cell.Blocks.OfType<Paragraph>())
            {
                y += b.Style.Before;
                var paragraphNode = cellNode?.Add("P");
                foreach (var line in Break(b, TextWidth(t, cell, width), _ => ""))
                {
                    Place(line, x + t.MarginLeft, y, paragraphNode, null, artifact: rowNode is null);
                    y += line.Height;
                }

                y += b.Style.After;
            }

            if (cell.Top is { } top) _page.Ops.Add(new LineOp(x, _y, x + width, _y, top.Width));
            if (cell.Bottom is { } bottom) _page.Ops.Add(new LineOp(x, _y + height, x + width, _y + height, bottom.Width));
            if (cell.Left is { } left) _page.Ops.Add(new LineOp(x, _y, x, _y + height, left.Width));
            if (cell.Right is { } right) _page.Ops.Add(new LineOp(x + width, _y, x + width, _y + height, right.Width));
            x += width;
        }

        _y += height;
        _pageHasContent = true;
    }

    // ---- Headers and footers: laid out per page, once the number of pages is known ----

    private double Height(IReadOnlyList<Block> blocks) =>
        blocks.OfType<Paragraph>().Sum(p => p.Style.Before + Break(p, ContentWidth, _ => "9").Sum(l => l.Height) + p.Style.After);

    private void Decorate(int index, int count)
    {
        var page = _pages[index];
        var firstPage = index == 0 && _section.TitlePage;
        var header = firstPage ? _section.FirstHeader : _section.Header;
        var footer = firstPage ? _section.FirstFooter : _section.Footer;
        string Field(string name) => name switch { "PAGE" => (index + 1).ToString(), "NUMPAGES" => count.ToString(), _ => "" };
        var saved = _page;
        _page = page;
        var y = _section.HeaderDistance;
        foreach (var p in header.OfType<Paragraph>()) y = Story(p, y, Field);
        y = _section.Height - _section.FooterDistance - footer.OfType<Paragraph>().Sum(p => p.Style.Before + Break(p, ContentWidth, Field).Sum(l => l.Height) + p.Style.After);
        foreach (var p in footer.OfType<Paragraph>()) y = Story(p, y, Field);
        _page = saved;
    }

    private double Story(Paragraph p, double y, Func<string, string> field)
    {
        y += p.Style.Before;
        foreach (var line in Break(p, ContentWidth, field))
        {
            Place(line, ContentLeft, y, null, null, artifact: true);
            y += line.Height;
        }

        return y + p.Style.After;
    }

    // ---- Breaking a paragraph into lines ----

    private sealed record Piece(string Text, RunStyle Style, double Width, PieceKind Kind, bool IsLabel = false);

    private enum PieceKind { Word, Space, Tab, Break }

    private sealed class Line
    {
        public List<(double X, Piece Piece)> Pieces { get; } = [];
        public double Start { get; set; }
        public double Limit { get; set; }
        public double Height { get; set; }
        public double Ascent { get; set; }
        public bool Last { get; set; }
        public bool EndsWithBreak { get; set; }
        public string Align { get; set; } = "left";
    }

    private List<Piece> Pieces(Paragraph p, Func<string, string> field)
    {
        var pieces = new List<Piece>();
        foreach (var inline in p.Inlines)
        {
            var face = fonts.Face(inline.Style);
            switch (inline)
            {
                case TabInline: pieces.Add(new Piece("\t", inline.Style, 0, PieceKind.Tab)); break;
                case BreakInline: pieces.Add(new Piece("\n", inline.Style, 0, PieceKind.Break)); break;
                case TextInline or FieldInline:
                    var text = inline is TextInline t ? t.Text : field(((FieldInline)inline).Field);
                    for (var i = 0; i < text.Length;)
                    {
                        var space = text[i] == ' ';
                        var j = i;
                        while (j < text.Length && (text[j] == ' ') == space) j++;
                        var chunk = text[i..j];
                        pieces.Add(new Piece(chunk, inline.Style, face.Width(chunk, inline.Style.Size), space ? PieceKind.Space : PieceKind.Word));
                        i = j;
                    }

                    break;
            }
        }

        return pieces;
    }

    private List<Line> Break(Paragraph p, double width, Func<string, string> field)
    {
        var s = p.Style;
        var lines = new List<Line>();
        var limit = width - s.Right;
        Line NewLine(double start)
        {
            var line = new Line { Start = start, Limit = limit, Align = s.Align };
            lines.Add(line);
            return line;
        }

        var line = NewLine(s.Left + s.FirstLine);
        var x = line.Start;
        if (p.Label is TextInline label)
        {
            var w = fonts.Face(label.Style).Width(label.Text, label.Style.Size);
            line.Pieces.Add((x, new Piece(label.Text, label.Style, w, PieceKind.Word, IsLabel: true)));
            x = NextTab(x + w, s, hanging: true);
        }

        foreach (var piece in Pieces(p, field))
        {
            switch (piece.Kind)
            {
                case PieceKind.Break:
                    line.EndsWithBreak = true;
                    line = NewLine(s.Left);
                    x = line.Start;
                    break;
                case PieceKind.Tab:
                    var stop = NextTab(x, s, hanging: false);
                    if (stop > limit) { line = NewLine(s.Left); x = line.Start; }
                    else x = stop;
                    break;
                case PieceKind.Space:
                    line.Pieces.Add((x, piece));
                    x += piece.Width;
                    break;
                default:
                    if (x + piece.Width > limit + 0.01 && line.Pieces.Any(q => q.Piece.Kind == PieceKind.Word && !q.Piece.IsLabel))
                    {
                        line = NewLine(s.Left);
                        x = line.Start;
                    }

                    line.Pieces.Add((x, piece));
                    x += piece.Width;
                    break;
            }
        }

        lines[^1].Last = true;
        foreach (var l in lines)
        {
            var styles = l.Pieces.Select(q => q.Piece.Style).DefaultIfEmpty(p.MarkStyle);
            var natural = styles.Max(st => fonts.Face(st).LineHeight(st.Size));
            l.Ascent = styles.Max(st => fonts.Face(st).Ascent(st.Size));
            l.Height = s.LineRule switch
            {
                "exact" => s.Line / 20,
                "atLeast" => Math.Max(s.Line / 20, natural),
                _ => natural * s.Line / 240,
            };
        }

        return lines;
    }

    private static double NextTab(double x, ParagraphStyle s, bool hanging)
    {
        var stops = new List<double>(s.Tabs);
        if (hanging && s.FirstLine < 0) stops.Add(s.Left);
        foreach (var stop in stops.Order())
            if (stop > x + 0.01) return stop;
        return (Math.Floor(x / DefaultTab) + 1) * DefaultTab;
    }

    // ---- Putting a line on the page ----

    private void Place(Line line, double left, double top, StructNode? node, StructNode? labelNode, bool artifact = false)
    {
        var words = line.Pieces.Where(q => q.Piece.Kind == PieceKind.Word).ToList();
        if (words.Count == 0) return;
        var end = words[^1].X + words[^1].Piece.Width;
        var shift = line.Align switch
        {
            "center" => (line.Limit - end) / 2 + (line.Start > 0 ? 0 : 0),
            "right" => line.Limit - end,
            _ => 0.0,
        };
        var justify = line.Align == "both" && !line.Last && !line.EndsWithBreak;
        var gaps = justify ? line.Pieces.Count(q => q.Piece.Kind == PieceKind.Space && q.X < end) : 0;
        var extra = gaps > 0 ? (line.Limit - end) / gaps : 0;
        var baseline = top + line.Ascent;
        var added = 0.0;
        var trailingSpacesFrom = line.Pieces.FindLastIndex(q => q.Piece.Kind == PieceKind.Word) + 1;
        for (var i = 0; i < line.Pieces.Count; i++)
        {
            var (x, piece) = line.Pieces[i];
            if (piece.Kind == PieceKind.Space)
            {
                // A space is real text, so copying and screen readers keep the words apart; justified lines widen them.
                if (i < trailingSpacesFrom) Emit(new TextOp(left + x + shift + added, baseline, piece.Text, piece.Style, artifact ? null : node));
                if (justify && x < end) added += extra;
                continue;
            }

            var owner = artifact ? null : piece.IsLabel ? labelNode ?? node : node;
            Emit(new TextOp(left + x + shift + added, baseline, piece.Text, piece.Style, owner));
        }
    }

    private void Emit(TextOp op)
    {
        // Join a run of text in one style and place with what came before, so the content stream stays small.
        if (_page.Ops.Count > 0 && _page.Ops[^1] is TextOp last && last.Node == op.Node && last.Style == op.Style &&
            Math.Abs(last.Baseline - op.Baseline) < 0.001 &&
            Math.Abs(last.X + fonts.Face(last.Style).Width(last.Text, last.Style.Size) - op.X) < 0.001)
        {
            _page.Ops[^1] = last with { Text = last.Text + op.Text };
            return;
        }

        _page.Ops.Add(op);
    }
}
