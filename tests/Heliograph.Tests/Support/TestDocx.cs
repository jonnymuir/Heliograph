using System.IO.Compression;
using System.Text;
using System.Xml.Linq;

namespace Heliograph.Tests.Support;

/// <summary>Builds a small .docx in memory, and reads the text back out of one, so tests work on real files.</summary>
internal sealed class TestDocx
{
    private const string Word = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";
    private const string OfficeRelationships = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
    private const string MainType = "application/vnd.openxmlformats-officedocument.wordprocessingml.document.main+xml";

    private readonly List<string> _paragraphs = [];
    private readonly List<(string Name, string Content)> _extraParts = [];
    private readonly List<(string Name, byte[] Bytes)> _binaryParts = [];
    private readonly List<string> _contentTypes = [];
    private readonly List<string> _documentRelationships = [];
    private string? _header;
    private string? _footer;
    private string? _rawHeader;
    private string? _author;
    private string? _title = "Decision letter";
    private string _wordNamespace = Word;
    private readonly List<string> _rootRelationships = [];
    private string _mainContentType = MainType;

    /// <summary>A document with one paragraph per string, each in a single run.</summary>
    public static TestDocx With(params string[] paragraphs)
    {
        var docx = new TestDocx();
        foreach (var paragraph in paragraphs)
        {
            docx.Paragraph(paragraph);
        }

        return docx;
    }

    /// <summary>Adds a paragraph whose text is spread over several runs, the way an editor stores edited text.</summary>
    public TestDocx Paragraph(params string[] runs)
    {
        _paragraphs.Add(ParagraphXml(runs));
        return this;
    }

    /// <summary>Adds a paragraph from raw WordprocessingML, for content no editor would write.</summary>
    public TestDocx RawParagraph(string xml)
    {
        _paragraphs.Add(xml);
        return this;
    }

    public TestDocx Header(string text)
    {
        _header = text;
        return this;
    }

    /// <summary>A header built from raw WordprocessingML, for one with more than a single line of text.</summary>
    public TestDocx RawHeader(string xml)
    {
        _header = "";
        _rawHeader = xml;
        return this;
    }

    public TestDocx Footer(string text)
    {
        _footer = text;
        return this;
    }

    public TestDocx Part(string name, string content, string? contentType = null)
    {
        _extraParts.Add((name, content));
        if (contentType is not null)
        {
            _contentTypes.Add($"<Override PartName=\"/{name}\" ContentType=\"{contentType}\"/>");
        }

        return this;
    }

    /// <summary>Adds a file such as a picture, with its content type.</summary>
    public TestDocx Media(string name, byte[] bytes, string contentType)
    {
        _binaryParts.Add((name, bytes));
        _contentTypes.Add($"<Override PartName=\"/{name}\" ContentType=\"{contentType}\"/>");
        return this;
    }

    /// <summary>Records who wrote and last saved the document, the way an editor does in its properties.</summary>
    public TestDocx Author(string name, string title = "Decision letter")
    {
        _author = name;
        _title = title;
        return this;
    }

    /// <summary>Leaves the document's properties without a title, as a document nobody has given one.</summary>
    public TestDocx Untitled()
    {
        _title = null;
        return this;
    }

    /// <summary>Uses another namespace for the document's own elements, as "Strict Open XML" does.</summary>
    public TestDocx WordNamespace(string wordNamespace)
    {
        _wordNamespace = wordNamespace;
        return this;
    }

    /// <summary>Adds a relationship from the package itself, such as to its extended properties.</summary>
    public TestDocx RootRelationship(string type, string target)
    {
        _rootRelationships.Add($"<Relationship Id=\"rIdR{_rootRelationships.Count}\" Type=\"{type}\" Target=\"{target}\"/>");
        return this;
    }

    public TestDocx MainContentType(string contentType)
    {
        _mainContentType = contentType;
        return this;
    }

    public TestDocx Relationship(string type, string target, bool external)
    {
        var mode = external ? " TargetMode=\"External\"" : "";
        _documentRelationships.Add($"<Relationship Id=\"rIdX{_documentRelationships.Count}\" Type=\"{type}\" Target=\"{target}\"{mode}/>");
        return this;
    }

    public MemoryStream Build()
    {
        var relationships = new StringBuilder();
        var overrides = new StringBuilder();
        if (_header is not null)
        {
            relationships.Append($"<Relationship Id=\"rIdH\" Type=\"{OfficeRelationships}/header\" Target=\"header1.xml\"/>");
            overrides.Append("<Override PartName=\"/word/header1.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.wordprocessingml.header+xml\"/>");
        }

        if (_footer is not null)
        {
            relationships.Append($"<Relationship Id=\"rIdF\" Type=\"{OfficeRelationships}/footer\" Target=\"footer1.xml\"/>");
            overrides.Append("<Override PartName=\"/word/footer1.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.wordprocessingml.footer+xml\"/>");
        }

        var stream = new MemoryStream();
        using (var zip = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            Add(zip, "[Content_Types].xml",
                "<Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\">"
                + "<Default Extension=\"rels\" ContentType=\"application/vnd.openxmlformats-package.relationships+xml\"/>"
                + "<Default Extension=\"xml\" ContentType=\"application/xml\"/>"
                + $"<Override PartName=\"/word/document.xml\" ContentType=\"{_mainContentType}\"/>"
                + "<Override PartName=\"/docProps/core.xml\" ContentType=\"application/vnd.openxmlformats-package.core-properties+xml\"/>"
                + overrides + string.Concat(_contentTypes) + "</Types>");
            Add(zip, "_rels/.rels",
                "<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">"
                + $"<Relationship Id=\"rId1\" Type=\"{OfficeRelationships}/officeDocument\" Target=\"word/document.xml\"/>"
                + "<Relationship Id=\"rId2\" Type=\"http://schemas.openxmlformats.org/package/2006/relationships/metadata/core-properties\" Target=\"docProps/core.xml\"/>"
                + string.Concat(_rootRelationships) + "</Relationships>");
            Add(zip, "docProps/core.xml",
                "<cp:coreProperties xmlns:cp=\"http://schemas.openxmlformats.org/package/2006/metadata/core-properties\" xmlns:dc=\"http://purl.org/dc/elements/1.1/\">"
                + $"<dc:title>{_title}</dc:title><dc:creator>{_author}</dc:creator><cp:lastModifiedBy>{_author}</cp:lastModifiedBy></cp:coreProperties>");

            Add(zip, "word/_rels/document.xml.rels",
                "<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">"
                + relationships + string.Concat(_documentRelationships) + "</Relationships>");
            Add(zip, "word/document.xml", $"<w:document xmlns:w=\"{_wordNamespace}\"><w:body>{string.Concat(_paragraphs)}</w:body></w:document>");
            if (_header is not null)
            {
                Add(zip, "word/header1.xml", $"<w:hdr xmlns:w=\"{Word}\">{_rawHeader ?? ParagraphXml([_header])}</w:hdr>");
            }

            if (_footer is not null)
            {
                Add(zip, "word/footer1.xml", $"<w:ftr xmlns:w=\"{Word}\">{ParagraphXml([_footer])}</w:ftr>");
            }

            foreach (var (name, content) in _extraParts)
            {
                Add(zip, name, content);
            }

            foreach (var (name, bytes) in _binaryParts)
            {
                using var entry = zip.CreateEntry(name).Open();
                entry.Write(bytes);
            }
        }

        stream.Position = 0;
        return stream;
    }

    /// <summary>The text of each paragraph in the document body, with a line break shown as a new line.</summary>
    public static IReadOnlyList<string> Paragraphs(byte[] docx) => PartParagraphs(docx, "word/document.xml");

    /// <summary>All the text in the headers and footers, one string per paragraph.</summary>
    public static IReadOnlyList<string> HeaderAndFooterParagraphs(byte[] docx)
    {
        using var zip = new ZipArchive(new MemoryStream(docx), ZipArchiveMode.Read);
        var names = zip.Entries.Select(e => e.FullName)
            .Where(n => n.StartsWith("word/header", StringComparison.Ordinal) || n.StartsWith("word/footer", StringComparison.Ordinal))
            .ToList();
        return names.SelectMany(name => PartParagraphs(docx, name)).ToList();
    }

    public static IReadOnlyList<string> PartParagraphs(byte[] docx, string partName)
    {
        using var zip = new ZipArchive(new MemoryStream(docx), ZipArchiveMode.Read);
        using var part = zip.GetEntry(partName)!.Open();
        XNamespace w = Word;
        return XDocument.Load(part).Descendants(w + "p").Select(paragraph => string.Concat(
            paragraph.Descendants().Where(e => e.Name == w + "t" || e.Name == w + "br")
                .Select(e => e.Name == w + "br" ? "\n" : e.Value))).ToList();
    }

    /// <summary>The style of each paragraph in the document body, or null where a paragraph names none.</summary>
    public static IReadOnlyList<string?> ParagraphStyles(byte[] docx)
    {
        XNamespace w = Word;
        return Body(docx).Descendants(w + "p")
            .Select(p => (string?)p.Element(w + "pPr")?.Element(w + "pStyle")?.Attribute(w + "val")).ToList();
    }

    /// <summary>Every table row in the document body, as the text of each of its cells.</summary>
    public static IReadOnlyList<string[]> TableRows(byte[] docx)
    {
        XNamespace w = Word;
        return Body(docx).Descendants(w + "tr").Select(row => row.Elements(w + "tc").Select(cell => string.Join("\n",
            cell.Elements(w + "p").Select(p => string.Concat(p.Descendants(w + "t").Select(t => t.Value))))).ToArray()).ToList();
    }

    /// <summary>Every table row in the document body as one line of text, with a bar between its cells.</summary>
    public static IReadOnlyList<string> TableLines(byte[] docx) => [.. TableRows(docx).Select(row => string.Join(" | ", row))];

    /// <summary>How many paragraphs each table cell in the document body holds. A cell with none is not a valid cell.</summary>
    public static IReadOnlyList<int> CellParagraphCounts(byte[] docx)
    {
        XNamespace w = Word;
        return Body(docx).Descendants(w + "tc").Select(cell => cell.Elements(w + "p").Count()).ToList();
    }

    /// <summary>All the text in the document body that is set in italics.</summary>
    public static string ItalicText(byte[] docx)
    {
        XNamespace w = Word;
        return string.Concat(Body(docx).Descendants(w + "r")
            .Where(run => run.Element(w + "rPr")?.Element(w + "i") is not null)
            .SelectMany(run => run.Elements(w + "t")).Select(text => text.Value));
    }

    private static XDocument Body(byte[] docx)
    {
        using var zip = new ZipArchive(new MemoryStream(docx), ZipArchiveMode.Read);
        using var part = zip.GetEntry("word/document.xml")!.Open();
        return XDocument.Load(part);
    }

    /// <summary>A copy of a .docx with one of its XML parts changed.</summary>
    public static byte[] WithPart(byte[] docx, string partName, Action<XDocument> change)
    {
        var output = new MemoryStream();
        using (var source = new ZipArchive(new MemoryStream(docx), ZipArchiveMode.Read))
        using (var target = new ZipArchive(output, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var entry in source.Entries)
            {
                using var from = entry.Open();
                using var to = target.CreateEntry(entry.FullName).Open();
                if (entry.FullName != partName)
                {
                    from.CopyTo(to);
                    continue;
                }

                var document = XDocument.Load(from, LoadOptions.PreserveWhitespace);
                change(document);
                document.Save(to, SaveOptions.DisableFormatting);
            }
        }

        return output.ToArray();
    }

    /// <summary>The raw text of one part of the file.</summary>
    public static string PartText(byte[] docx, string partName)
    {
        using var zip = new ZipArchive(new MemoryStream(docx), ZipArchiveMode.Read);
        using var reader = new StreamReader(zip.GetEntry(partName)!.Open());
        return reader.ReadToEnd();
    }

    /// <summary>The text of one of the document's properties, such as "creator" or "lastModifiedBy".</summary>
    public static string? Property(byte[] docx, string name)
    {
        using var zip = new ZipArchive(new MemoryStream(docx), ZipArchiveMode.Read);
        using var part = zip.GetEntry("docProps/core.xml")!.Open();
        return XDocument.Load(part).Descendants().FirstOrDefault(e => e.Name.LocalName == name)?.Value;
    }

    private static string ParagraphXml(IEnumerable<string> runs) =>
        "<w:p>" + string.Concat(runs.Select(run =>
            $"<w:r><w:t xml:space=\"preserve\">{new XText(run)}</w:t></w:r>")) + "</w:p>";

    private static void Add(ZipArchive zip, string name, string content)
    {
        using var writer = new StreamWriter(zip.CreateEntry(name).Open(), new UTF8Encoding(false));
        writer.Write(content);
    }
}
