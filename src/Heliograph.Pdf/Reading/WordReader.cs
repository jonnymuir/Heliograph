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
    private readonly Dictionary<string, string> _relationshipTypes;
    private readonly bool _largerSpacing;
    private readonly bool _evenPages;
    private readonly NoteNumbers _notes;
    private readonly NoteNumbers _endnotes;
    private string _part = "word/document.xml";

    private WordReader(byte[] docx)
    {
        _package = new PackageParts(docx);
        var relationships = _package.Part("word/_rels/document.xml.rels")?.Root?.Elements().ToList() ?? [];
        _relationships = relationships.ToDictionary(e => (string)e.Attribute("Id")!, e => (string)e.Attribute("Target")!);
        _relationshipTypes = relationships.ToDictionary(e => (string)e.Attribute("Id")!, e => (string?)e.Attribute("Type") ?? "");
        var theme = relationships.FirstOrDefault(e => ((string?)e.Attribute("Type"))?.EndsWith("/theme", StringComparison.Ordinal) == true);
        _theme = ThemeFonts.Read(theme is null ? null : _package.Part("word/" + (string)theme.Attribute("Target")!));
        Styles = new StyleSheet(Part("word/styles.xml"));
        // Word's interval between default tab stops is half an inch unless the document sets its own.
        var settings = _package.Part("word/settings.xml")?.Root;
        var defaultTab = Twips(settings?.Element(W + "defaultTabStop"), "val") ?? 36;
        // A new Word document leaves this out, and then the space between paragraphs is the larger of the two, not the sum.
        _largerSpacing = !On(settings?.Element(W + "compat")?.Element(W + "doNotUseHTMLParagraphAutoSpacing"));
        _evenPages = On(settings?.Element(W + "evenAndOddHeaders"));
        (_notes, _endnotes) = NoteNumbering(settings);
        _paragraphs = new ParagraphReader(Styles, new NumberingDefinitions(Part("word/numbering.xml")), defaultTab,
            new PartLinks(id => _package.Linked(_part, id), id => _package.External(_part, id)) { Notes = _notes, Endnotes = _endnotes });
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
        var sections = new List<Section>();
        Stories? before = null;
        foreach (var (sect, elements) in SectionParts(body))
        {
            var stories = StoriesOf(sect, before);
            sections.Add(new Section(PageGeometry.Of(sect), stories) { Numbers = Numbers(sect), Body = Blocks(elements) });
            before = stories;
        }

        var title = _package.Part("docProps/core.xml")?.Root?.Element(Dc + "title")?.Value ?? "";
        return new WordDocument(title, Styles.Language, sections)
        {
            Notes = Notes(),
            Endnotes = NotesIn(NoteKind.Endnote, _endnotes),
            NoteSeparator = _paragraphs.Read(new XElement(W + "p", new XElement(W + "pPr", new XElement(W + "spacing",
                new XAttribute(W + "before", 0), new XAttribute(W + "after", 0), new XAttribute(W + "line", 240), new XAttribute(W + "lineRule", "auto"))))),
        };
    }

    // The body split at each section break: a paragraph whose properties hold a section's settings ends that section, and
    // the body's own settings are the last section's.
    private static IEnumerable<(XElement Settings, List<XElement> Elements)> SectionParts(XElement body)
    {
        var elements = new List<XElement>();
        foreach (var e in body.Elements().Where(e => e.Name != W + "sectPr"))
        {
            elements.Add(e);
            if (e.Element(W + "pPr")?.Element(W + "sectPr") is { } settings)
            {
                yield return (settings, elements);
                elements = [];
            }
        }

        yield return (body.Element(W + "sectPr") ?? new XElement(W + "sectPr"), elements);
    }

    // A section's headers and footers: each it does not set is the section before's, as in Word. The first page has its
    // own only when the section says so.
    private Stories StoriesOf(XElement sect, Stories? before)
    {
        var titlePage = On(sect.Element(W + "titlePg"));
        var firstHeader = Carried(sect, "header", "first", before?.InheritedFirstHeader);
        var firstFooter = Carried(sect, "footer", "first", before?.InheritedFirstFooter);
        return new Stories(Carried(sect, "header", "default", before?.Header), Carried(sect, "footer", "default", before?.Footer),
            titlePage ? firstHeader : null, titlePage ? firstFooter : null)
        {
            InheritedFirstHeader = firstHeader,
            InheritedFirstFooter = firstFooter,
            EvenHeader = _evenPages ? Carried(sect, "header", "even", before?.EvenHeader) : null,
            EvenFooter = _evenPages ? Carried(sect, "footer", "even", before?.EvenFooter) : null,
        };
    }

    // A header or footer the section sets, else the one it carries on from the section before.
    private IReadOnlyList<Block> Carried(XElement sect, string kind, string type, IReadOnlyList<Block>? before) => Story(sect, kind, type) ?? before ?? [];

    private static PageNumbers Numbers(XElement sect)
    {
        var type = sect.Element(W + "pgNumType");
        return new PageNumbers((int?)type?.Attribute(W + "start"), (string?)type?.Attribute(W + "fmt") ?? "decimal");
    }

    // Notes are numbered as the document's settings say, and as its sections say over that, as Word writes it.
    private (NoteNumbers Footnotes, NoteNumbers Endnotes) NoteNumbering(XElement? settings)
    {
        var last = _package.Part("word/document.xml")?.Root?.Element(W + "body")?.Element(W + "sectPr");
        return (Numbering(NoteKind.Footnote, settings, last), Numbering(NoteKind.Endnote, settings, last));
    }

    // How the document numbers its footnotes or endnotes: the format, and the number the first takes, each from the
    // section's settings or else the document's.
    private static NoteNumbers Numbering(NoteKind kind, params XElement?[] layers)
    {
        string? Setting(string setting) => layers.Select(l => Val(l?.Element(W + kind.Settings)?.Element(W + setting))).LastOrDefault(v => v is not null);
        return new(Setting("numFmt") ?? kind.Format, int.TryParse(Setting("numStart"), out var start) ? start : 1);
    }

    // The paragraphs of each footnote the body refers to, each opening with its number.
    private Dictionary<string, IReadOnlyList<Block>> Notes() => NotesIn(NoteKind.Footnote, _notes).ToDictionary(n => n.Id, n => n.Blocks, StringComparer.Ordinal);

    // The paragraphs of each note of a kind the body refers to, in the order first referred to.
    private List<(string Id, IReadOnlyList<Block> Blocks)> NotesIn(NoteKind kind, NoteNumbers numbers)
    {
        var notes = new List<(string Id, IReadOnlyList<Block> Blocks)>();
        var target = _relationships.FirstOrDefault(r => _relationshipTypes.GetValueOrDefault(r.Key)?.EndsWith("/" + kind.Part, StringComparison.Ordinal) == true).Value;
        var name = target is null ? null : "word/" + target;
        if ((name is null ? null : Part(name)?.Root) is not { } root)
        {
            return notes;
        }

        _part = name!;
        foreach (var id in numbers.Order.ToList())
        {
            numbers.Current = id;
            var note = root.Elements(W + kind.Element).FirstOrDefault(f => (string?)f.Attribute(W + "id") == id);
            notes.Add((id, note is null ? [] : Blocks(note)));
        }

        (numbers.Current, _part) = (null, "word/document.xml");
        return notes;
    }

    private IReadOnlyList<Block> Blocks(XElement container) => Blocks(container.Elements());

    private List<Block> Blocks(IEnumerable<XElement> elements)
    {
        var blocks = new List<Block>();
        foreach (var e in elements)
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
            else if (e.Name == W + "customXml")
            {
                blocks.AddRange(Blocks(e));
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
}
