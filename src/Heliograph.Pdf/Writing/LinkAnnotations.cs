using System.Text;
using Heliograph.Pdf.Layout;
using static Heliograph.Pdf.Writing.PdfObjects;

namespace Heliograph.Pdf.Writing;

/// <summary>
/// Each link area as a link annotation, as PDF/UA asks: going to its address, described by it, borderless, and tied into
/// the structure (its Link holds the annotation, and the parent tree leads from the annotation back to its Link).
/// </summary>
internal sealed class LinkAnnotations(PdfObjects objects, int firstKey)
{
    /// <summary>Each annotation's key in the parent tree, and the Link it belongs to.</summary>
    public List<(int Key, StructNode Node)> Parents { get; } = [];

    /// <summary>The page's /Annots entry for its link areas, or nothing when it has none.</summary>
    public string Write(int pageIndex, int pageId, IReadOnlyList<LinkOp> links, double pageHeight)
    {
        var ids = new List<int>();
        foreach (var link in links)
        {
            var (id, key) = (objects.Reserve(), firstKey + Parents.Count);
            var rect = Invariant($"[{N(link.X1)} {N(pageHeight - link.Bottom)} {N(link.X2)} {N(pageHeight - link.Top)}]");
            var address = Literal(link.Address);
            objects.Set(id, Invariant($"<</Type /Annot /Subtype /Link /P {pageId} 0 R /Rect {rect} /Border [0 0 0] /F 4 /Contents {address} ")
                + Invariant($"/StructParent {key} /A <</Type /Action /S /URI /URI {address}>>>>"));
            link.Node.Kids.Add(new AnnotationRef(pageIndex, id));
            Parents.Add((key, link.Node));
            ids.Add(id);
        }

        return ids.Count == 0 ? "" : Invariant($" /Annots [{string.Join(" ", ids.Select(i => Invariant($"{i} 0 R")))}]");
    }

    // An ASCII string as a PDF literal string, with its brackets and backslashes escaped.
    private static string Literal(string text)
    {
        var escaped = new StringBuilder("(");
        foreach (var c in text)
        {
            escaped.Append(c is '(' or ')' or '\\' ? $"\\{c}" : c.ToString());
        }

        return escaped.Append(')').ToString();
    }
}
