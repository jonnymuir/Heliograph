using System.Xml.Linq;

namespace Heliograph.Documents;

/// <summary>The few pieces of WordprocessingML the engine builds for itself.</summary>
internal static class WordXml
{
    public static readonly XNamespace Word = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";

    /// <summary>The namespace of what a compiled template holds in place of tags. It never appears in a file.</summary>
    public static readonly XNamespace Slot = "urn:heliograph:compiled";

    /// <summary>An empty run with the same formatting.</summary>
    public static XElement LikeRun(XElement run) =>
        new(Word + "r", run.Element(Word + "rPr") is { } formatting ? new XElement(formatting) : null);

    /// <summary>An empty paragraph with the same style and layout, but not the section break, which only one may hold.</summary>
    public static XElement LikeParagraph(XElement paragraph)
    {
        var like = new XElement(Word + "p");
        if (paragraph.Element(Word + "pPr") is { } properties)
        {
            like.Add(new XElement(properties.Name, properties.Elements().Where(e => e.Name != Word + "sectPr")));
        }

        return like;
    }

    /// <summary>Whether a run holds anything besides its formatting.</summary>
    public static bool HasContent(XElement run) => run.Elements().Any(e => e.Name != Word + "rPr");

    public static void Move(IEnumerable<XNode> nodes, XElement to)
    {
        foreach (var node in nodes.ToList())
        {
            node.Remove();
            to.Add(node);
        }
    }
}
