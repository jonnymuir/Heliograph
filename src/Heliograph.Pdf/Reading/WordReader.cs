using System.IO.Compression;
using System.Xml;
using System.Xml.Linq;
using static Heliograph.Pdf.Reading.WordXml;

namespace Heliograph.Pdf.Reading;

/// <summary>Reads a rendered .docx into a <see cref="WordDocument"/>.</summary>
internal sealed class WordReader : IDisposable
{
    private static readonly XmlReaderSettings Safe = new() { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null };
    private readonly ZipArchive _zip;
    private readonly ParagraphReader _paragraphs;
    private readonly Dictionary<string, string> _relationships;
    private readonly bool _largerSpacing;
    private string _part = "word/document.xml";

    private WordReader(byte[] docx)
    {
        _zip = new ZipArchive(new MemoryStream(docx), ZipArchiveMode.Read);
        Styles = new StyleSheet(Part("word/styles.xml"));
        // Word's interval between default tab stops is half an inch unless the document sets its own.
        var settings = Part("word/settings.xml")?.Root;
        var defaultTab = Twips(settings?.Element(W + "defaultTabStop"), "val") ?? 36;
        // A new Word document leaves this out, and then the space between paragraphs is the larger of the two, not the sum.
        _largerSpacing = !On(settings?.Element(W + "compat")?.Element(W + "doNotUseHTMLParagraphAutoSpacing"));
        _paragraphs = new ParagraphReader(Styles, new NumberingDefinitions(Part("word/numbering.xml")), defaultTab, id => Linked(_part, id));
        _relationships = (Part("word/_rels/document.xml.rels")?.Root?.Elements() ?? [])
            .ToDictionary(e => (string)e.Attribute("Id")!, e => (string)e.Attribute("Target")!);
    }

    private StyleSheet Styles { get; }

    public static WordDocument Read(byte[] docx)
    {
        using var reader = new WordReader(docx);
        return reader.Document();
    }

    public void Dispose() => _zip.Dispose();

    private WordDocument Document()
    {
        var body = Part("word/document.xml")?.Root?.Element(W + "body") ?? throw new InvalidDataException("The document has no body.");
        var sect = body.Element(W + "sectPr") ?? new XElement(W + "sectPr");
        var titlePage = On(sect.Element(W + "titlePg"));
        var stories = new Stories(
            Story(sect, "header", "default") ?? [], Story(sect, "footer", "default") ?? [],
            titlePage ? Story(sect, "header", "first") ?? [] : null, titlePage ? Story(sect, "footer", "first") ?? [] : null);
        var title = Part("docProps/core.xml")?.Root?.Element(Dc + "title")?.Value ?? "";
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

        if (Part("word/" + target)?.Root is not { } root)
        {
            return null;
        }

        // A header's pictures are found through the header's own relationships.
        _part = "word/" + target;
        var blocks = Blocks(root);
        _part = "word/document.xml";
        return blocks;
    }

    // The bytes of what a part links to by relationship id, inside the package only.
    private byte[]? Linked(string partName, string id)
    {
        var folder = partName[..(partName.LastIndexOf('/') + 1)];
        var relationship = Part($"{folder}_rels/{partName[folder.Length..]}.rels")?.Root?.Elements()
            .FirstOrDefault(e => (string?)e.Attribute("Id") == id && (string?)e.Attribute("TargetMode") != "External");
        if ((string?)relationship?.Attribute("Target") is not { } target)
        {
            return null;
        }

        var name = target.StartsWith('/') ? target.TrimStart('/') : Normalise(folder + target);
        if (_zip.GetEntry(name) is not { } entry)
        {
            return null;
        }

        using var stream = entry.Open();
        using var bytes = new MemoryStream();
        stream.CopyTo(bytes);
        return bytes.ToArray();
    }

    // "word/../media/a.png" as "media/a.png".
    private static string Normalise(string path)
    {
        var segments = new List<string>();
        foreach (var segment in path.Split('/'))
        {
            if (segment == ".." && segments.Count > 0)
            {
                segments.RemoveAt(segments.Count - 1);
            }
            else if (segment is not ("." or ".." or ""))
            {
                segments.Add(segment);
            }
        }

        return string.Join('/', segments);
    }

    private XDocument? Part(string name)
    {
        if (_zip.GetEntry(name) is not { } entry)
        {
            return null;
        }

        using var stream = entry.Open();
        using var reader = XmlReader.Create(stream, Safe);
        return XDocument.Load(reader);
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
