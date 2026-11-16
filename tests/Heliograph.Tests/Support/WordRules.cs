using System.IO.Compression;
using System.Xml.Linq;

namespace Heliograph.Tests.Support;

/// <summary>
/// The rules Word enforces that the schema validator does not: Word reports a file that breaks one as unreadable.
/// They are written here from knowledge of the format, separately from the engine, so that the engine is not
/// marking its own work.
/// </summary>
internal static class WordRules
{
    private static readonly XNamespace W = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";
    private static readonly string[] StoryNames = ["hdr", "ftr", "footnote", "endnote", "txbxContent"];

    public static IReadOnlyList<string> Problems(byte[] docx)
    {
        var problems = new List<string>();
        using var zip = new ZipArchive(new MemoryStream(docx), ZipArchiveMode.Read);
        foreach (var entry in zip.Entries.Where(e => e.FullName.StartsWith("word/", StringComparison.Ordinal) && e.FullName.EndsWith(".xml", StringComparison.Ordinal)))
        {
            using var stream = entry.Open();
            problems.AddRange(Problems(XDocument.Load(stream)).Select(problem => $"{entry.FullName}: {problem}"));
        }

        return problems;
    }

    private static IEnumerable<string> Problems(XDocument part)
    {
        var elements = part.Descendants().ToList();
        return Leftovers(elements).Concat(Tables(elements)).Concat(Stories(elements)).Concat(Fields(elements));
    }

    private static IEnumerable<string> Leftovers(List<XElement> elements)
    {
        if (elements.Any(e => e.Name.NamespaceName.StartsWith("urn:heliograph", StringComparison.Ordinal)))
        {
            yield return "something the compiler wrote for itself was left in the file";
        }
    }

    private static IEnumerable<string> Tables(List<XElement> elements)
    {
        foreach (var cell in elements.Where(e => e.Name == W + "tc" && e.Elements().LastOrDefault()?.Name != W + "p"))
        {
            yield return $"a table cell does not end with a paragraph: {Excerpt(cell.Parent!)}";
        }

        if (elements.Any(e => e.Name == W + "tbl" && !e.Elements(W + "tr").Any()))
        {
            yield return "a table has no rows";
        }

        if (elements.Any(e => e.Name == W + "tr" && !e.Elements(W + "tc").Any()))
        {
            yield return "a table row has no cells";
        }
    }

    /// <summary>A header, footer, note or text box must hold at least one paragraph or table.</summary>
    private static IEnumerable<string> Stories(List<XElement> elements) => elements
        .Where(e => e.Name.Namespace == W && StoryNames.Contains(e.Name.LocalName))
        .Where(story => !story.Elements().Any(e => e.Name == W + "p" || e.Name == W + "tbl"))
        .Select(story => $"a {story.Name.LocalName} has no paragraph in it");

    /// <summary>A field has a beginning and an end, in that order, and one field may sit inside another.</summary>
    private static IEnumerable<string> Fields(List<XElement> elements)
    {
        var depth = 0;
        foreach (var type in elements.Where(e => e.Name == W + "fldChar").Select(e => (string?)e.Attribute(W + "fldCharType")))
        {
            depth += type == "begin" ? 1 : type == "end" ? -1 : 0;
            if (depth < 0)
            {
                yield return "a field ends before it begins";
                yield break;
            }
        }

        if (depth != 0)
        {
            yield return "a field begins and never ends";
        }
    }

    private static string Excerpt(XElement element)
    {
        var text = string.Concat(element.Descendants(W + "t").Select(t => t.Value));
        return text.Length <= 40 ? text : text[..40] + "...";
    }
}
