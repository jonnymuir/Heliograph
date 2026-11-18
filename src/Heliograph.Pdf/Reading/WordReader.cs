using System.Xml.Linq;
using static Heliograph.Pdf.Reading.WordXml;

namespace Heliograph.Pdf.Reading;

/// <summary>Reads a rendered .docx into a <see cref="WordDocument"/>.</summary>
internal sealed class WordReader : IDisposable
{
    private readonly PackageParts _package;
    private readonly ThemeFonts _theme;
    private readonly ParagraphReader _paragraphs;
    private readonly Dictionary<string, string> _relationships;
    private readonly bool _largerSpacing;
    private string _part = "word/document.xml";

    private WordReader(byte[] docx)
    {
        _package = new PackageParts(docx);
        var relationships = _package.Part("word/_rels/document.xml.rels")?.Root?.Elements().ToList() ?? [];
        _relationships = relationships.ToDictionary(e => (string)e.Attribute("Id")!, e => (string)e.Attribute("Target")!);
        var theme = relationships.FirstOrDefault(e => ((string?)e.Attribute("Type"))?.EndsWith("/theme", StringComparison.Ordinal) == true);
        _theme = ThemeFonts.Read(theme is null ? null : _package.Part("word/" + (string)theme.Attribute("Target")!));
        Styles = new StyleSheet(Part("word/styles.xml"));
        // Word's interval between default tab stops is half an inch unless the document sets its own.
        var settings = _package.Part("word/settings.xml")?.Root;
        var defaultTab = Twips(settings?.Element(W + "defaultTabStop"), "val") ?? 36;
        // A new Word document leaves this out, and then the space between paragraphs is the larger of the two, not the sum.
        _largerSpacing = !On(settings?.Element(W + "compat")?.Element(W + "doNotUseHTMLParagraphAutoSpacing"));
        _paragraphs = new ParagraphReader(Styles, new NumberingDefinitions(Part("word/numbering.xml")), defaultTab,
            new PartLinks(id => _package.Linked(_part, id), id => _package.External(_part, id)));
    }

    private StyleSheet Styles { get; }

    // A part with its theme fonts written as the fonts they stand for.
    private XDocument? Part(string name) => _theme.Resolve(_package.Part(name));

    public static WordDocument Read(byte[] docx)
    {
        using var reader = new WordReader(docx);
        return reader.Document();
    }

    public void Dispose() => _package.Dispose();

    private WordDocument Document()
    {
        var body = Part("word/document.xml")?.Root?.Element(W + "body") ?? throw new InvalidDataException("The document has no body.");
        var sect = body.Element(W + "sectPr") ?? new XElement(W + "sectPr");
        var titlePage = On(sect.Element(W + "titlePg"));
        var stories = new Stories(
            Story(sect, "header", "default") ?? [], Story(sect, "footer", "default") ?? [],
            titlePage ? Story(sect, "header", "first") ?? [] : null, titlePage ? Story(sect, "footer", "first") ?? [] : null);
        var title = _package.Part("docProps/core.xml")?.Root?.Element(Dc + "title")?.Value ?? "";
        return new WordDocument(title, Styles.Language, new Section(Geometry(sect), stories), Blocks(body));
    }

    private IReadOnlyList<Block> Blocks(XElement container)
    {
        var blocks = new List<Block>();
        foreach (var e in container.Elements())
        {
            if (e.Name == W + "p")
            {
                var p = _paragraphs.Read(e);
                if (blocks.LastOrDefault() is Paragraph above)
                {
                    (blocks[^1], p) = ParagraphSpacing.Between(above, p, _largerSpacing);
                }

                blocks.Add(p);
            }
            else if (e.Name == W + "tbl")
            {
                blocks.Add(Table(e));
            }
            else if (e.Name == W + "sdt" && e.Element(W + "sdtContent") is { } content)
            {
                blocks.AddRange(Blocks(content));
            }
        }

        return blocks;
    }

    // A table, whose style gives the paragraphs in its cells properties of their own.
    private Table Table(XElement tbl)
    {
        var style = new TableStyle(tbl, Styles);
        _paragraphs.Table = style;
        var table = TableReader.Read(tbl, style, Blocks);
        _paragraphs.Table = null;
        return table;
    }

    private IReadOnlyList<Block>? Story(XElement sect, string kind, string type)
    {
        var reference = sect.Elements(W + kind + "Reference").FirstOrDefault(e => (string?)e.Attribute(W + "type") == type);
        var target = reference is null ? null : _relationships.GetValueOrDefault((string)reference.Attribute(R + "id")!);
        return target is null ? null : StoryIn("word/" + target);
    }

    // A header's or footer's blocks; its pictures are found through its own relationships.
    private IReadOnlyList<Block>? StoryIn(string name)
    {
        if (Part(name)?.Root is not { } root)
        {
            return null;
        }

        _part = name;
        var blocks = Blocks(root);
        _part = "word/document.xml";
        return blocks;
    }

    private static PageGeometry Geometry(XElement sect)
    {
        var size = sect.Element(W + "pgSz");
        var margin = sect.Element(W + "pgMar");
        // A document that leaves its page out is laid out on Word's own default, as Word and LibreOffice do: US Letter with
        // inch margins and the header and footer half an inch from the edge.
        return new PageGeometry(
            Twips(size, "w") ?? 612, Twips(size, "h") ?? 792,
            Twips(margin, "top") ?? 72, Twips(margin, "bottom") ?? 72, Twips(margin, "left") ?? 72, Twips(margin, "right") ?? 72,
            Twips(margin, "header") ?? 36, Twips(margin, "footer") ?? 36);
    }
}
