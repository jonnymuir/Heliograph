using System.Xml.Linq;

namespace Heliograph.Documents;

/// <summary>
/// The rules a part must keep for Word to open the file, beyond what the format's schema says. The compiler checks
/// them on a rehearsal of every outcome, so a template that would break them is refused before any letter is made.
/// </summary>
internal static class WordStructure
{
    private static readonly XNamespace Word = WordXml.Word;
    private static readonly HashSet<string> Stories = ["hdr", "ftr", "footnote", "endnote", "txbxContent"];

    /// <summary>The first rule the part breaks, in plain words, or null.</summary>
    public static string? Problem(XDocument part)
    {
        var elements = part.Descendants().ToList();
        return Leftover(elements) ?? Table(elements) ?? Story(elements) ?? Field(elements);
    }

    private static string? Leftover(List<XElement> elements) =>
        elements.Any(e => e.Name.Namespace == WordXml.Slot) ? "a tag was not filled in or removed" : null;

    private static string? Table(List<XElement> elements)
    {
        if (elements.Any(e => e.Name == Word + "tc" && e.Elements().LastOrDefault()?.Name != Word + "p"))
        {
            return "a table cell does not end with a paragraph";
        }

        if (elements.Any(e => e.Name == Word + "tbl" && !e.Elements(Word + "tr").Any()))
        {
            return "a table has no rows";
        }

        return elements.Any(e => e.Name == Word + "tr" && !e.Elements(Word + "tc").Any()) ? "a table row has no cells" : null;
    }

    private static string? Story(List<XElement> elements) => elements
        .Where(e => e.Name.Namespace == Word && Stories.Contains(e.Name.LocalName))
        .Any(story => !story.Elements().Any(e => e.Name == Word + "p" || e.Name == Word + "tbl"))
        ? "a header, footer, note or text box has nothing in it"
        : null;

    private static string? Field(List<XElement> elements)
    {
        var depth = 0;
        foreach (var type in elements.Where(e => e.Name == Word + "fldChar").Select(e => (string?)e.Attribute(Word + "fldCharType")))
        {
            depth += type == "begin" ? 1 : type == "end" ? -1 : 0;
            if (depth < 0)
            {
                return "a field ends before it begins";
            }
        }

        return depth == 0 ? null : "a field begins and never ends";
    }
}
