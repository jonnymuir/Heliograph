using System.Xml.Linq;
using static Heliograph.Pdf.Reading.WordXml;

namespace Heliograph.Pdf.Reading;

/// <summary>Reads a rendered .docx into a <see cref="WordDocument"/>.</summary>
internal sealed class WordReader : IDisposable
{
    private readonly PackageParts _package;
    private readonly ParagraphReader _paragraphs;
    private readonly Dictionary<string, string> _relationships;
    private readonly bool _largerSpacing;
    private string _part = "word/document.xml";

    private WordReader(byte[] docx)
    {
        _package = new PackageParts(docx);
        Styles = new StyleSheet(_package.Part("word/styles.xml"));
        // Word's interval between default tab stops is half an inch unless the document sets its own.
        var settings = _package.Part("word/settings.xml")?.Root;
        var defaultTab = Twips(settings?.Element(W + "defaultTabStop"), "val") ?? 36;
        // A new Word document leaves this out, and then the space between paragraphs is the larger of the two, not the sum.
        _largerSpacing = !On(settings?.Element(W + "compat")?.Element(W + "doNotUseHTMLParagraphAutoSpacing"));
        _paragraphs = new ParagraphReader(Styles, new NumberingDefinitions(_package.Part("word/numbering.xml")), defaultTab, id => _package.Linked(_part, id));
        _relationships = (_package.Part("word/_rels/document.xml.rels")?.Root?.Elements() ?? [])
            .ToDictionary(e => (string)e.Attribute("Id")!, e => (string)e.Attribute("Target")!);
    }

    private StyleSheet Styles { get; }

    public static WordDocument Read(byte[] docx)
    {
        using var reader = new WordReader(docx);
        return reader.Document();
    }

    public void Dispose() => _package.Dispose();

    private WordDocument Document()
    {
        var body = _package.Part("word/document.xml")?.Root?.Element(W + "body") ?? throw new InvalidDataException("The document has no body.");
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
                blocks.Add(Spaced(_paragraphs.Read(e), blocks.LastOrDefault()));
            }
            else if (e.Name == W + "tbl")
            {
                blocks.Add(TableReader.Read(e, Blocks));
            }
            else if (e.Name == W + "sdt" && e.Element(W + "sdtContent") is { } content)
            {
                blocks.AddRange(Blocks(content));
            }
        }

        return blocks;
    }

    private Paragraph Spaced(Paragraph p, Block? above) => _largerSpacing && above is Paragraph prev
        ? p with { Style = p.Style with { Overlap = Math.Min(p.Style.Spacing.Before, prev.Style.Spacing.After) } }
        : p;

    private IReadOnlyList<Block>? Story(XElement sect, string kind, string type)
    {
        var reference = sect.Elements(W + kind + "Reference").FirstOrDefault(e => (string?)e.Attribute(W + "type") == type);
        if (reference is null || !_relationships.TryGetValue((string)reference.Attribute(R + "id")!, out var target))
        {
            return null;
        }

        if (_package.Part("word/" + target)?.Root is not { } root)
        {
            return null;
        }

        // A header's pictures are found through the header's own relationships.
        _part = "word/" + target;
        var blocks = Blocks(root);
        _part = "word/document.xml";
        return blocks;
    }

    private static PageGeometry Geometry(XElement sect)
    {
        var size = sect.Element(W + "pgSz");
        var margin = sect.Element(W + "pgMar");
        return new PageGeometry(
            Twips(size, "w") ?? 595.3, Twips(size, "h") ?? 841.9,
            Twips(margin, "top") ?? 72, Twips(margin, "bottom") ?? 72, Twips(margin, "left") ?? 72, Twips(margin, "right") ?? 72,
            Twips(margin, "header") ?? 35.4, Twips(margin, "footer") ?? 35.4);
    }
}
