using System.Xml.Linq;

namespace Heliograph.Documents;

/// <summary>
/// Puts right what hiding content can leave behind and what is always safe to put right. The format requires a
/// paragraph in places that a condition may have emptied, and half a bookmark is debris.
/// </summary>
internal static class DocumentMender
{
    private static readonly XNamespace Word = WordXml.Word;

    // Each of these must hold at least one paragraph or table.
    private static readonly HashSet<string> Stories = ["hdr", "ftr", "footnote", "endnote", "txbxContent"];

    public static void Mend(XDocument part)
    {
        var elements = part.Descendants().Where(e => e.Name.Namespace == Word).ToList();
        RemoveHalfBookmarks(elements);

        // A table cell must end with a paragraph, even when everything in it was hidden.
        foreach (var cell in elements.Where(e => e.Name == Word + "tc" && e.Elements().LastOrDefault()?.Name != Word + "p"))
        {
            cell.Add(new XElement(Word + "p"));
        }

        foreach (var story in elements.Where(e => Stories.Contains(e.Name.LocalName) && !e.Elements().Any(IsBlock)))
        {
            story.Add(new XElement(Word + "p"));
        }
    }

    private static bool IsBlock(XElement element) => element.Name == Word + "p" || element.Name == Word + "tbl";

    /// <summary>A bookmark's start and end can be in different places, so a condition can hide one and not the other.</summary>
    private static void RemoveHalfBookmarks(List<XElement> elements)
    {
        var starts = elements.Where(e => e.Name == Word + "bookmarkStart" && e.Parent is not null).ToLookup(Id);
        var ends = elements.Where(e => e.Name == Word + "bookmarkEnd" && e.Parent is not null).ToLookup(Id);
        foreach (var half in starts.Where(s => !ends.Contains(s.Key)).Concat(ends.Where(e => !starts.Contains(e.Key))).SelectMany(group => group).ToList())
        {
            half.Remove();
        }
    }

    private static string? Id(XElement bookmark) => (string?)bookmark.Attribute(Word + "id");
}
