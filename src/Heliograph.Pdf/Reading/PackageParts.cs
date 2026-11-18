using System.IO.Compression;
using System.Xml;
using System.Xml.Linq;

namespace Heliograph.Pdf.Reading;

/// <summary>A .docx's parts: each read as XML with no document type definitions, and the files a part links to.</summary>
internal sealed class PackageParts(byte[] docx) : IDisposable
{
    private static readonly XmlReaderSettings Safe = new() { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null };
    private readonly ZipArchive _zip = new(new MemoryStream(docx), ZipArchiveMode.Read);

    public XDocument? Part(string name)
    {
        if (_zip.GetEntry(name) is not { } entry)
        {
            return null;
        }

        using var stream = entry.Open();
        using var reader = XmlReader.Create(stream, Safe);
        return XDocument.Load(reader);
    }

    /// <summary>The bytes of what a part links to by relationship id, inside the package only.</summary>
    public byte[]? Linked(string partName, string id)
    {
        var folder = partName[..(partName.LastIndexOf('/') + 1)];
        var relationship = Part($"{folder}_rels/{partName[folder.Length..]}.rels")?.Root?.Elements()
            .FirstOrDefault(e => (string?)e.Attribute("Id") == id && (string?)e.Attribute("TargetMode") != "External");
        if ((string?)relationship?.Attribute("Target") is not { } target
            || _zip.GetEntry(target.StartsWith('/') ? target.TrimStart('/') : Normalise(folder + target)) is not { } entry)
        {
            return null;
        }

        using var stream = entry.Open();
        using var bytes = new MemoryStream();
        stream.CopyTo(bytes);
        return bytes.ToArray();
    }

    public void Dispose() => _zip.Dispose();

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
}
