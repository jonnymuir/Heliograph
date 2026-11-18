using System.IO.Compression;
using System.Text;
using System.Xml;
using System.Xml.Linq;

namespace Heliograph.Documents;

/// <summary>
/// A .docx read into memory within fixed limits, and written back out the same way every time. A .docx is a zip of
/// XML parts (ISO/IEC 29500, Open Packaging Conventions).
/// </summary>
internal sealed class DocxPackage
{
    public const long MaxUnpackedBytes = 64L * 1024 * 1024;
    public const int MaxEntries = 2000;

    private const string OfficeDocumentRelationship = "/officeDocument";
    private const string MainDocumentType = "application/vnd.openxmlformats-officedocument.wordprocessingml.document.main+xml";

    // A zip entry carries a timestamp. A fixed one keeps the same template and data giving the same bytes.
    private static readonly DateTimeOffset FixedTimestamp = new(1980, 1, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly XNamespace Relationships = "http://schemas.openxmlformats.org/package/2006/relationships";
    private static readonly XNamespace ContentTypes = "http://schemas.openxmlformats.org/package/2006/content-types";

    // The first bytes of the container that both the older .doc format and a password-protected file use.
    private static readonly byte[] OldWordSignature = [0xD0, 0xCF, 0x11, 0xE0];

    // The parts, besides the main document, whose paragraphs an author writes, by the last word of their relationship type.
    private static readonly Dictionary<string, string> TextPartLabels = new()
    {
        ["header"] = "header", ["footer"] = "footer", ["footnotes"] = "footnotes", ["endnotes"] = "endnotes",
    };

    private readonly List<(string Name, byte[] Bytes)> _entries;

    private DocxPackage(List<(string Name, byte[] Bytes)> entries) => _entries = entries;

    public IEnumerable<string> Names => _entries.Select(entry => entry.Name);

    /// <summary>Reads the zip, or says why it was refused. Stops reading as soon as a limit is passed.</summary>
    public static DocxPackage? Read(Stream stream, out DocxRefusal? refusal)
    {
        refusal = null;
        try
        {
            var file = ReadWithin(stream, MaxUnpackedBytes);
            if (file is null)
            {
                refusal = DocxRefusal.TooLarge;
                return null;
            }

            if (file.AsSpan().StartsWith(OldWordSignature))
            {
                refusal = DocxRefusal.OldOrProtected;
                return null;
            }

            using var zip = new ZipArchive(new MemoryStream(file), ZipArchiveMode.Read);
            if (zip.Entries.Count > MaxEntries)
            {
                refusal = DocxRefusal.TooLarge;
                return null;
            }

            var entries = new List<(string, byte[])>();
            var remaining = MaxUnpackedBytes;
            foreach (var entry in zip.Entries)
            {
                using var source = entry.Open();
                var bytes = ReadWithin(source, remaining);
                if (bytes is null)
                {
                    refusal = DocxRefusal.TooLarge;
                    return null;
                }

                remaining -= bytes.Length;
                entries.Add((entry.FullName, bytes));
            }

            return new DocxPackage(entries);
        }
        catch (InvalidDataException)
        {
            refusal = DocxRefusal.NotAWordDocument;
            return null;
        }
    }

    public bool Has(string name) => _entries.Any(entry => entry.Name == name);

    /// <summary>A part's bytes, or null when there is no such part.</summary>
    public byte[]? Bytes(string name) => _entries.FirstOrDefault(entry => entry.Name == name).Bytes;

    /// <summary>Parses a part as XML with document type definitions prohibited, so no entity is ever resolved.</summary>
    public XDocument? Xml(string name)
    {
        var bytes = _entries.FirstOrDefault(entry => entry.Name == name).Bytes;
        if (bytes is null)
        {
            return null;
        }

        var settings = new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null };
        using var reader = XmlReader.Create(new MemoryStream(bytes), settings);
        return XDocument.Load(reader, LoadOptions.PreserveWhitespace);
    }

    /// <summary>Every relationship in a relationships part: its type, its target, and whether the target is outside the file.</summary>
    public IEnumerable<(string Type, string Target, bool External)> RelationshipsIn(string relationshipsPart) =>
        (Xml(relationshipsPart)?.Root?.Elements(Relationships + "Relationship") ?? [])
        .Select(r => ((string?)r.Attribute("Type") ?? "", (string?)r.Attribute("Target") ?? "",
            string.Equals((string?)r.Attribute("TargetMode"), "External", StringComparison.OrdinalIgnoreCase)));

    /// <summary>Every content type the package declares.</summary>
    public IEnumerable<(string? PartName, string ContentType)> DeclaredContentTypes() =>
        (Xml("[Content_Types].xml")?.Root?.Elements() ?? [])
        .Where(e => e.Name.Namespace == ContentTypes)
        .Select(e => (((string?)e.Attribute("PartName"))?.TrimStart('/'), (string?)e.Attribute("ContentType") ?? ""));

    /// <summary>
    /// The parts that hold text an author wrote: the main document, then its headers, footers and notes. Null when the
    /// package is not a Word document.
    /// </summary>
    public IReadOnlyList<(string Name, string Label)>? TextParts()
    {
        if (MainDocument() is not { } main)
        {
            return null;
        }

        var folder = main.Contains('/', StringComparison.Ordinal) ? main[..(main.LastIndexOf('/') + 1)] : "";
        var others = RelationshipsIn($"{folder}_rels/{main[folder.Length..]}.rels")
            .Where(r => !r.External)
            .Select(r => (Name: r.Target.StartsWith('/') ? r.Target.TrimStart('/') : folder + r.Target,
                Label: TextPartLabels.FirstOrDefault(known => r.Type.EndsWith("/" + known.Key, StringComparison.Ordinal)).Value))
            .Where(part => part.Label is not null && Has(part.Name));
        return [(main, "document"), .. others];
    }

    /// <summary>The name of the main document part, when the package says it has one and that it is a Word document.</summary>
    private string? MainDocument()
    {
        var main = RelationshipsIn("_rels/.rels")
            .Where(r => !r.External && r.Type.EndsWith(OfficeDocumentRelationship, StringComparison.Ordinal))
            .Select(r => r.Target.TrimStart('/'))
            .FirstOrDefault();
        var isWord = main is not null && Has(main) && DeclaredContentTypes().Any(t => t.PartName == main && t.ContentType == MainDocumentType);
        return isWord ? main : null;
    }

    /// <summary>Writes the package with some parts replaced: same order, same timestamps, same bytes for the same input.</summary>
    public byte[] Save(IReadOnlyDictionary<string, XDocument> replaced) => Save(replaced, new Dictionary<string, XDocument>());

    /// <summary>Writes the package with some parts replaced and new parts added at the end.</summary>
    public byte[] Save(IReadOnlyDictionary<string, XDocument> replaced, IReadOnlyDictionary<string, XDocument> added)
    {
        var output = new MemoryStream();
        using (var zip = new ZipArchive(output, ZipArchiveMode.Create, leaveOpen: true))
        {
            var newParts = added.Where(part => !Has(part.Key)).Select(part => (part.Key, Array.Empty<byte>()));
            var all = replaced.Concat(added).ToDictionary(part => part.Key, part => part.Value);
            foreach (var (name, bytes) in _entries.Concat(newParts))
            {
                var entry = zip.CreateEntry(name, CompressionLevel.Optimal);
                entry.LastWriteTime = FixedTimestamp;
                using var stream = entry.Open();
                if (all.TryGetValue(name, out var xml))
                {
                    var settings = new XmlWriterSettings { Encoding = new UTF8Encoding(false), Indent = false };
                    using var writer = XmlWriter.Create(stream, settings);
                    xml.Save(writer);
                }
                else
                {
                    stream.Write(bytes);
                }
            }
        }

        return output.ToArray();
    }

    private static byte[]? ReadWithin(Stream source, long limit)
    {
        // The size in the zip's directory is whatever the file claims, so count what actually comes out.
        using var buffer = new MemoryStream();
        var chunk = new byte[81920];
        int read;
        while ((read = source.Read(chunk, 0, chunk.Length)) > 0)
        {
            if (buffer.Length + read > limit)
            {
                return null;
            }

            buffer.Write(chunk, 0, read);
        }

        return buffer.ToArray();
    }
}

/// <summary>Why a file could not be read as a package at all.</summary>
internal enum DocxRefusal
{
    NotAWordDocument,
    OldOrProtected,
    TooLarge,
}
