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

    private readonly List<(string Name, byte[] Bytes)> _entries;

    private DocxPackage(List<(string Name, byte[] Bytes)> entries) => _entries = entries;

    public IEnumerable<string> Names => _entries.Select(entry => entry.Name);

    /// <summary>Reads the zip, or says why it was refused. Stops reading as soon as a limit is passed.</summary>
    public static DocxPackage? Read(Stream stream, out string? refusalCode)
    {
        refusalCode = null;
        try
        {
            using var zip = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: true);
            if (zip.Entries.Count > MaxEntries)
            {
                refusalCode = DiagnosticCodes.TooLarge;
                return null;
            }

            var entries = new List<(string, byte[])>();
            var remaining = MaxUnpackedBytes;
            foreach (var entry in zip.Entries)
            {
                var bytes = ReadWithin(entry, remaining);
                if (bytes is null)
                {
                    refusalCode = DiagnosticCodes.TooLarge;
                    return null;
                }

                remaining -= bytes.Length;
                entries.Add((entry.FullName, bytes));
            }

            return new DocxPackage(entries);
        }
        catch (InvalidDataException)
        {
            refusalCode = DiagnosticCodes.NotAWordDocument;
            return null;
        }
    }

    public bool Has(string name) => _entries.Any(entry => entry.Name == name);

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
    /// The parts that hold text an author wrote: the main document, then its headers and footers. Null when the
    /// package is not a Word document.
    /// </summary>
    public IReadOnlyList<(string Name, string Label)>? TextParts()
    {
        var main = RelationshipsIn("_rels/.rels")
            .Where(r => !r.External && r.Type.EndsWith(OfficeDocumentRelationship, StringComparison.Ordinal))
            .Select(r => r.Target.TrimStart('/'))
            .FirstOrDefault();
        if (main is null || !Has(main) || !DeclaredContentTypes().Any(t => t.PartName == main && t.ContentType == MainDocumentType))
        {
            return null;
        }

        var folder = main.Contains('/', StringComparison.Ordinal) ? main[..(main.LastIndexOf('/') + 1)] : "";
        var parts = new List<(string, string)> { (main, "document") };
        foreach (var (type, target, external) in RelationshipsIn($"{folder}_rels/{main[folder.Length..]}.rels"))
        {
            var label = type.EndsWith("/header", StringComparison.Ordinal) ? "header"
                : type.EndsWith("/footer", StringComparison.Ordinal) ? "footer" : null;
            var name = target.StartsWith('/') ? target.TrimStart('/') : folder + target;
            if (label is not null && !external && Has(name))
            {
                parts.Add((name, label));
            }
        }

        return parts;
    }

    /// <summary>Writes the package with some parts replaced: same order, same timestamps, same bytes for the same input.</summary>
    public byte[] Save(IReadOnlyDictionary<string, XDocument> replaced)
    {
        var output = new MemoryStream();
        using (var zip = new ZipArchive(output, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var (name, bytes) in _entries)
            {
                var entry = zip.CreateEntry(name, CompressionLevel.Optimal);
                entry.LastWriteTime = FixedTimestamp;
                using var stream = entry.Open();
                if (replaced.TryGetValue(name, out var xml))
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

    private static byte[]? ReadWithin(ZipArchiveEntry entry, long limit)
    {
        // The size in the zip's directory is whatever the file claims, so count what actually comes out.
        using var source = entry.Open();
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
