using System.IO.Compression;
using System.Xml.Linq;

namespace Heliograph.PdfExperiment.OwnEngine;

// A rendered letter read into the few things a page needs: paragraphs with their resolved style, tables, and the
// section's page, margins, headers and footers. All lengths are in points. Anything not listed here is ignored, which
// is the experiment's question: is this much enough for real letters?

public sealed record RunStyle(bool Bold, bool Italic, bool Underline, double Size, string Font);

public abstract record Inline(RunStyle Style);
public sealed record TextInline(string Text, RunStyle Style) : Inline(Style);
public sealed record TabInline(RunStyle Style) : Inline(Style);
public sealed record BreakInline(RunStyle Style) : Inline(Style);
public sealed record FieldInline(string Field, RunStyle Style) : Inline(Style);

public sealed record ParagraphStyle(
    string StyleId, int? OutlineLevel, double Before, double After, double Line, string LineRule, string Align,
    double Left, double Right, double FirstLine, bool KeepNext, bool KeepLines, bool WidowControl, bool PageBreakBefore,
    IReadOnlyList<double> Tabs);

public abstract record Block;
public sealed record Paragraph(ParagraphStyle Style, IReadOnlyList<Inline> Inlines, Inline? Label, bool IsListItem, RunStyle MarkStyle) : Block;
public sealed record Border(double Width);
public sealed record Cell(IReadOnlyList<Block> Blocks, int Span, Border? Top, Border? Left, Border? Bottom, Border? Right);
public sealed record Row(IReadOnlyList<Cell> Cells, bool Header, bool CantSplit);
public sealed record Table(IReadOnlyList<double> Grid, IReadOnlyList<Row> Rows, double Indent, double MarginLeft, double MarginRight,
    double MarginTop, double MarginBottom) : Block;

public sealed record Section(double Width, double Height, double Top, double Bottom, double Left, double Right, double HeaderDistance,
    double FooterDistance, bool TitlePage, IReadOnlyList<Block> Header, IReadOnlyList<Block> Footer,
    IReadOnlyList<Block> FirstHeader, IReadOnlyList<Block> FirstFooter);

public sealed record WordDocument(string Title, string Language, Section Section, IReadOnlyList<Block> Body);

public static class Docx
{
    private static readonly XNamespace W = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";
    private static readonly XNamespace R = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
    private static readonly XNamespace Dc = "http://purl.org/dc/elements/1.1/";

    public static WordDocument Read(byte[] docx)
    {
        using var zip = new ZipArchive(new MemoryStream(docx), ZipArchiveMode.Read);
        XDocument? Part(string name) => zip.GetEntry(name) is { } e ? XDocument.Load(e.Open()) : null;

        var styles = new Styles(Part("word/styles.xml"));
        var numbering = new Numbering(Part("word/numbering.xml"));
        var reader = new Reader(styles, numbering);
        var document = Part("word/document.xml")!;
        var rels = (Part("word/_rels/document.xml.rels")?.Root?.Elements() ?? []).ToDictionary(e => (string)e.Attribute("Id")!, e => (string)e.Attribute("Target")!);
        var body = document.Root!.Element(W + "body")!;
        var sect = body.Element(W + "sectPr")!;

        IReadOnlyList<Block> Story(string kind, string type)
        {
            var reference = sect.Elements(W + kind + "Reference").FirstOrDefault(e => (string?)e.Attribute(W + "type") == type);
            if (reference is null || !rels.TryGetValue((string)reference.Attribute(R + "id")!, out var target)) return [];
            return reader.Blocks(Part("word/" + target)!.Root!);
        }

        double Tw(XElement? e, string attribute, double fallback) => e?.Attribute(W + attribute) is { } a ? double.Parse(a.Value) / 20 : fallback;
        var size = sect.Element(W + "pgSz");
        var margin = sect.Element(W + "pgMar");
        var section = new Section(
            Tw(size, "w", 595.3), Tw(size, "h", 841.9), Tw(margin, "top", 72), Tw(margin, "bottom", 72), Tw(margin, "left", 72), Tw(margin, "right", 72),
            Tw(margin, "header", 35.4), Tw(margin, "footer", 35.4), On(sect.Element(W + "titlePg")),
            Story("header", "default"), Story("footer", "default"), Story("header", "first"), Story("footer", "first"));

        var title = Part("docProps/core.xml")?.Root?.Element(Dc + "title")?.Value ?? "";
        return new WordDocument(title, styles.Language, section, reader.Blocks(body));
    }

    internal static bool On(XElement? e) => e is not null && (string?)e.Attribute(W + "val") is null or "true" or "1" or "on";

    private sealed class Reader(Styles styles, Numbering numbering)
    {
        public IReadOnlyList<Block> Blocks(XElement container)
        {
            var blocks = new List<Block>();
            foreach (var e in container.Elements())
            {
                if (e.Name == W + "p") blocks.Add(Paragraph(e));
                else if (e.Name == W + "tbl") blocks.Add(Table(e));
                else if (e.Name == W + "sdt") blocks.AddRange(Blocks(e.Element(W + "sdtContent")!));
            }

            return blocks;
        }

        private Paragraph Paragraph(XElement p)
        {
            var pPr = p.Element(W + "pPr");
            var styleId = (string?)pPr?.Element(W + "pStyle")?.Attribute(W + "val") ?? styles.DefaultParagraph;
            var chain = styles.ParagraphChain(styleId);
            var numPr = pPr?.Element(W + "numPr") ?? chain.Select(s => s.Element(W + "pPr")?.Element(W + "numPr")).LastOrDefault(n => n is not null);
            var level = numbering.Level(numPr);
            var properties = new List<XElement?>(chain.Select(s => s.Element(W + "pPr"))) { level?.Element(W + "pPr"), pPr };
            var paragraphStyle = ParagraphStyle(styleId, properties.Where(x => x is not null).Cast<XElement>().ToList());
            var baseRun = new List<XElement?> { styles.DefaultRun };
            baseRun.AddRange(chain.Select(s => s.Element(W + "rPr")));

            var inlines = new List<Inline>();
            string? field = null;
            var inResult = false;
            foreach (var r in p.Descendants().Where(d => d.Name == W + "r" || d.Name == W + "fldSimple"))
            {
                if (r.Name == W + "fldSimple")
                {
                    inlines.Add(new FieldInline(FieldName((string)r.Attribute(W + "instr")!), RunStyle(baseRun, null)));
                    continue;
                }

                if (r.Ancestors(W + "fldSimple").Any()) continue;
                var style = RunStyle(baseRun, r.Element(W + "rPr"));
                foreach (var c in r.Elements())
                {
                    if (c.Name == W + "fldChar")
                    {
                        var type = (string?)c.Attribute(W + "fldCharType");
                        if (type == "begin") field = "";
                        else if (type == "separate") { inlines.Add(new FieldInline(FieldName(field ?? ""), style)); inResult = true; }
                        else if (type == "end") { field = null; inResult = false; }
                    }
                    else if (c.Name == W + "instrText" && field is not null) field += c.Value;
                    else if (inResult) continue;
                    else if (c.Name == W + "t") inlines.Add(new TextInline(c.Value, style));
                    else if (c.Name == W + "tab") inlines.Add(new TabInline(style));
                    else if (c.Name == W + "br" || c.Name == W + "cr") inlines.Add(new BreakInline(style));
                }
            }

            Inline? label = null;
            if (level is not null && numbering.Label(numPr!, level) is { } text)
            {
                var labelStyle = RunStyle(baseRun, level.Element(W + "rPr"));
                label = new TextInline(text, labelStyle with { Font = "Liberation Sans" });
            }

            return new Paragraph(paragraphStyle, inlines, label, level is not null && label is not null, RunStyle(baseRun, pPr?.Element(W + "rPr")));
        }

        private static string FieldName(string instruction) => instruction.Trim().Split(' ', 2)[0].ToUpperInvariant();

        private RunStyle RunStyle(List<XElement?> chain, XElement? direct)
        {
            var all = new List<XElement?>(chain);
            if (direct?.Element(W + "rStyle") is { } rs) all.AddRange(styles.CharacterChain((string)rs.Attribute(W + "val")!).Select(s => s.Element(W + "rPr")));
            all.Add(direct);
            bool bold = false, italic = false, underline = false;
            double size = 10;
            var font = "Liberation Sans";
            foreach (var x in all.Where(x => x is not null))
            {
                if (x!.Element(W + "b") is { } b) bold = On(b);
                if (x.Element(W + "i") is { } i) italic = On(i);
                if (x.Element(W + "u") is { } u) underline = (string?)u.Attribute(W + "val") is not ("none" or null);
                if (x.Element(W + "sz")?.Attribute(W + "val") is { } sz) size = double.Parse(sz.Value) / 2;
                if (x.Element(W + "rFonts")?.Attribute(W + "ascii") is { } f) font = f.Value;
            }

            return new RunStyle(bold, italic, underline, size, font);
        }

        private static ParagraphStyle ParagraphStyle(string styleId, List<XElement> pPrs)
        {
            int? outline = null;
            double before = 0, after = 0, line = 240, left = 0, right = 0, first = 0;
            string rule = "auto", align = "left";
            bool keepNext = false, keepLines = false, widow = true, pageBreak = false;
            var tabs = new List<double>();
            foreach (var x in pPrs)
            {
                if (x.Element(W + "outlineLvl")?.Attribute(W + "val") is { } o) outline = int.Parse(o.Value);
                if (x.Element(W + "spacing") is { } s)
                {
                    if (s.Attribute(W + "before") is { } bf) before = double.Parse(bf.Value) / 20;
                    if (s.Attribute(W + "after") is { } af) after = double.Parse(af.Value) / 20;
                    if (s.Attribute(W + "line") is { } ln) line = double.Parse(ln.Value);
                    if (s.Attribute(W + "lineRule") is { } lr) rule = lr.Value;
                }

                if (x.Element(W + "ind") is { } ind)
                {
                    if ((ind.Attribute(W + "left") ?? ind.Attribute(W + "start")) is { } l) left = double.Parse(l.Value) / 20;
                    if ((ind.Attribute(W + "right") ?? ind.Attribute(W + "end")) is { } r) right = double.Parse(r.Value) / 20;
                    if (ind.Attribute(W + "hanging") is { } h) first = -double.Parse(h.Value) / 20;
                    if (ind.Attribute(W + "firstLine") is { } fl) first = double.Parse(fl.Value) / 20;
                }

                if (x.Element(W + "jc")?.Attribute(W + "val") is { } jc) align = jc.Value switch { "start" => "left", "end" => "right", var v => v };
                if (x.Element(W + "keepNext") is { } kn) keepNext = On(kn);
                if (x.Element(W + "keepLines") is { } kl) keepLines = On(kl);
                if (x.Element(W + "widowControl") is { } wc) widow = On(wc);
                if (x.Element(W + "pageBreakBefore") is { } pb) pageBreak = On(pb);
                if (x.Element(W + "tabs") is { } t)
                    foreach (var tab in t.Elements(W + "tab"))
                    {
                        var pos = double.Parse((string)tab.Attribute(W + "pos")!) / 20;
                        if ((string?)tab.Attribute(W + "val") == "clear") tabs.Remove(pos);
                        else if ((string?)tab.Attribute(W + "val") != "num") tabs.Add(pos);
                    }
            }

            tabs.Sort();
            return new ParagraphStyle(styleId, outline, before, after, line, rule, align, left, right, first, keepNext, keepLines, widow, pageBreak, tabs);
        }

        private Table Table(XElement tbl)
        {
            var tblPr = tbl.Element(W + "tblPr");
            double Tw(XElement? e, double fallback) => e?.Attribute(W + "w") is { } a ? double.Parse(a.Value) / 20 : fallback;
            var mar = tblPr?.Element(W + "tblCellMar");
            var grid = tbl.Element(W + "tblGrid")!.Elements(W + "gridCol").Select(g => double.Parse((string)g.Attribute(W + "w")!) / 20).ToList();
            var tableBorders = tblPr?.Element(W + "tblBorders");
            var rows = new List<Row>();
            foreach (var tr in tbl.Elements(W + "tr"))
            {
                var trPr = tr.Element(W + "trPr");
                var cells = tr.Elements(W + "tc").Select(tc =>
                {
                    var tcPr = tc.Element(W + "tcPr");
                    var borders = tcPr?.Element(W + "tcBorders");
                    Border? B(string side) => BorderOf(borders?.Element(W + side) ?? tableBorders?.Element(W + side));
                    var span = (int?)tcPr?.Element(W + "gridSpan")?.Attribute(W + "val") ?? 1;
                    return new Cell(Blocks(tc), span, B("top"), B("left"), B("bottom"), B("right"));
                }).ToList();
                rows.Add(new Row(cells, On(trPr?.Element(W + "tblHeader")), On(trPr?.Element(W + "cantSplit"))));
            }

            return new Table(grid, rows, Tw(tblPr?.Element(W + "tblInd"), 0), Tw(mar?.Element(W + "left"), 5.4), Tw(mar?.Element(W + "right"), 5.4),
                Tw(mar?.Element(W + "top"), 0), Tw(mar?.Element(W + "bottom"), 0));
        }

        private static Border? BorderOf(XElement? e) =>
            e is null || (string?)e.Attribute(W + "val") is "nil" or "none" ? null : new Border(double.Parse((string?)e.Attribute(W + "sz") ?? "4") / 8);
    }

    private sealed class Styles
    {
        private readonly Dictionary<string, XElement> _byId = [];

        public Styles(XDocument? styles)
        {
            if (styles?.Root is not { } root) return;
            foreach (var s in root.Elements(W + "style")) _byId[(string)s.Attribute(W + "styleId")!] = s;
            DefaultParagraph = root.Elements(W + "style").FirstOrDefault(s => (string?)s.Attribute(W + "type") == "paragraph" && (string?)s.Attribute(W + "default") is "1" or "true")
                ?.Attribute(W + "styleId")?.Value ?? "Normal";
            DefaultRun = root.Element(W + "docDefaults")?.Element(W + "rPrDefault")?.Element(W + "rPr");
            Language = (string?)DefaultRun?.Element(W + "lang")?.Attribute(W + "val") ?? "en-GB";
        }

        public string DefaultParagraph { get; } = "Normal";
        public XElement? DefaultRun { get; }
        public string Language { get; } = "en-GB";

        public List<XElement> ParagraphChain(string id) => Chain(id);
        public List<XElement> CharacterChain(string id) => Chain(id);

        private List<XElement> Chain(string id)
        {
            var chain = new List<XElement>();
            for (var current = id; current is not null && _byId.TryGetValue(current, out var style) && chain.Count < 20;
                 current = (string?)style.Element(W + "basedOn")?.Attribute(W + "val"))
                chain.Insert(0, style);
            return chain;
        }
    }

    private sealed class Numbering
    {
        private readonly Dictionary<string, XElement> _abstracts = [];
        private readonly Dictionary<string, string> _numToAbstract = [];
        private readonly Dictionary<(string Num, int Level), int> _counters = [];

        public Numbering(XDocument? numbering)
        {
            if (numbering?.Root is not { } root) return;
            foreach (var a in root.Elements(W + "abstractNum")) _abstracts[(string)a.Attribute(W + "abstractNumId")!] = a;
            foreach (var n in root.Elements(W + "num")) _numToAbstract[(string)n.Attribute(W + "numId")!] = (string)n.Element(W + "abstractNumId")!.Attribute(W + "val")!;
        }

        public XElement? Level(XElement? numPr)
        {
            if (numPr is null) return null;
            var numId = (string?)numPr.Element(W + "numId")?.Attribute(W + "val");
            var ilvl = (int?)numPr.Element(W + "ilvl")?.Attribute(W + "val") ?? 0;
            if (numId is null or "0" || !_numToAbstract.TryGetValue(numId, out var abs) || !_abstracts.TryGetValue(abs, out var a)) return null;
            return a.Elements(W + "lvl").FirstOrDefault(l => (int)l.Attribute(W + "ilvl")! == ilvl);
        }

        public string? Label(XElement numPr, XElement level)
        {
            var numId = (string)numPr.Element(W + "numId")!.Attribute(W + "val")!;
            var ilvl = (int)level.Attribute(W + "ilvl")!;
            var format = (string?)level.Element(W + "numFmt")?.Attribute(W + "val") ?? "decimal";
            if (format == "none") return null;
            var start = (int?)level.Element(W + "start")?.Attribute(W + "val") ?? 1;
            var count = _counters.TryGetValue((numId, ilvl), out var c) ? c + 1 : start;
            _counters[(numId, ilvl)] = count;
            foreach (var deeper in _counters.Keys.Where(k => k.Num == numId && k.Level > ilvl).ToList()) _counters.Remove(deeper);
            if (format == "bullet") return "•";
            var text = (string?)level.Element(W + "lvlText")?.Attribute(W + "val") ?? "%1.";
            return text.Replace($"%{ilvl + 1}", count.ToString());
        }
    }
}
