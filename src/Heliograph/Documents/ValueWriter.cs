using System.Text;
using System.Xml;
using System.Xml.Linq;

namespace Heliograph.Documents;

/// <summary>
/// Writes a value into the document in place of its slot. The text is data and stays data: it is written as text
/// content, never parsed.
/// </summary>
internal static class ValueWriter
{
    private static readonly XNamespace Word = TagScanner.Word;

    /// <summary>
    /// One paragraph goes into the slot's run. More than one splits the paragraph the slot is in: the text before
    /// the slot stays with the first, the text after it goes with the last, and each new paragraph keeps the
    /// original's style and the run's formatting.
    /// </summary>
    public static void Write(XElement slot, IReadOnlyList<string[]> paragraphs)
    {
        var run = slot.Parent!;
        var first = run.Name == Word + "r" ? run.Parent : null;
        if (paragraphs.Count == 1 || first?.Name != Word + "p")
        {
            // Not directly in a paragraph (inside a link, say), so a paragraph cannot be split here: a blank line instead.
            slot.ReplaceWith(Elements([.. paragraphs.SelectMany((lines, index) => index == 0 ? lines : lines.Prepend(""))]));
            return;
        }

        var last = Tail(first, run, slot);
        slot.ReplaceWith(Elements(paragraphs[0]));
        var previous = first;
        for (var index = 1; index < paragraphs.Count; index++)
        {
            var paragraph = index == paragraphs.Count - 1 ? last : LikeParagraph(first);
            var text = LikeRun(run);
            text.Add(Elements(paragraphs[index]));
            (paragraph.Element(Word + "pPr") is { } properties ? properties.AddAfterSelf : (Action<object>)paragraph.AddFirst)(text);
            previous.AddAfterSelf(paragraph);
            previous = paragraph;
        }
    }

    /// <summary>The paragraph that takes everything after the slot. A section break belongs to it, as the last one.</summary>
    private static XElement Tail(XElement paragraph, XElement run, XElement slot)
    {
        var tail = LikeParagraph(paragraph);
        var rest = LikeRun(run);
        Move(slot.NodesAfterSelf(), rest);
        tail.Add(rest);
        Move(run.NodesAfterSelf(), tail);
        if (paragraph.Element(Word + "pPr")?.Element(Word + "sectPr") is { } section)
        {
            section.Remove();
            (tail.Element(Word + "pPr") ?? AddProperties(tail)).Add(section);
        }

        return tail;
    }

    private static XElement AddProperties(XElement paragraph)
    {
        var properties = new XElement(Word + "pPr");
        paragraph.AddFirst(properties);
        return properties;
    }

    private static void Move(IEnumerable<XNode> nodes, XElement to)
    {
        foreach (var node in nodes.ToList())
        {
            node.Remove();
            to.Add(node);
        }
    }

    /// <summary>An empty paragraph with the same style and layout, but not the section break, which only one may hold.</summary>
    private static XElement LikeParagraph(XElement paragraph)
    {
        var like = new XElement(Word + "p");
        if (paragraph.Element(Word + "pPr") is { } properties)
        {
            like.Add(new XElement(properties.Name, properties.Elements().Where(e => e.Name != Word + "sectPr")));
        }

        return like;
    }

    /// <summary>An empty run with the same formatting.</summary>
    private static XElement LikeRun(XElement run) =>
        new(Word + "r", run.Element(Word + "rPr") is { } formatting ? new XElement(formatting) : null);

    /// <summary>The elements that print lines inside a run: text, with a line break between lines.</summary>
    private static IEnumerable<XElement> Elements(string[] lines)
    {
        for (var index = 0; index < lines.Length; index++)
        {
            if (index > 0)
            {
                yield return new XElement(Word + "br");
            }

            if (Printable(lines[index]) is { Length: > 0 } text)
            {
                yield return new XElement(Word + "t", new XAttribute(XNamespace.Xml + "space", "preserve"), text);
            }
        }
    }

    /// <summary>Drops the characters XML cannot hold (most control characters), which would make the file unreadable.</summary>
    private static string Printable(string text)
    {
        var kept = new StringBuilder(text.Length);
        for (var index = 0; index < text.Length; index++)
        {
            if (index + 1 < text.Length && XmlConvert.IsXmlSurrogatePair(text[index + 1], text[index]))
            {
                kept.Append(text[index]).Append(text[++index]);
            }
            else if (XmlConvert.IsXmlChar(text[index]))
            {
                kept.Append(text[index]);
            }
        }

        return kept.ToString();
    }
}
