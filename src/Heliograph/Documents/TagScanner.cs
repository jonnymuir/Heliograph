using System.Xml.Linq;

namespace Heliograph.Documents;

/// <summary>
/// Finds each tag in a part and swaps it for something the renderer acts on. An editor may store one tag in several
/// pieces of text, so a paragraph's text is joined before it is read.
/// </summary>
internal static class TagScanner
{
    private static readonly XNamespace Word = WordXml.Word;
    private static readonly XNamespace Slot = WordXml.Slot;

    /// <summary>The element a compiled template holds where a value is printed.</summary>
    public static readonly XName Value = WordXml.Slot + "value";

    private const int ExcerptLength = 80;

    public static void Compile(XDocument part, string label, FieldDictionary dictionary, List<Diagnostic> diagnostics)
    {
        // An uploaded file must not be able to plant a slot of its own.
        part.Descendants().Where(e => e.Name.Namespace == Slot).ToList().ForEach(e => e.Remove());

        var scan = new PartScan(label, dictionary, diagnostics);
        var paragraphs = part.Descendants(Word + "p").ToList();
        for (var index = 0; index < paragraphs.Count; index++)
        {
            scan.Paragraph(paragraphs[index], index + 1);
        }

        Conditions.Wrap(scan.Markers, diagnostics);
    }

    /// <summary>The scan of one part: what it is checked against, and what it has found so far.</summary>
    private sealed class PartScan(string label, FieldDictionary dictionary, List<Diagnostic> diagnostics)
    {
        public List<ConditionMarker> Markers { get; } = [];

        public void Paragraph(XElement paragraph, int number)
        {
            // A text box sits inside a paragraph and has paragraphs of its own, which get their own turn.
            var texts = paragraph.Descendants(Word + "t").Where(t => t.Ancestors(Word + "p").First() == paragraph).ToList();
            var joined = string.Concat(texts.Select(t => t.Value));
            if (!joined.Contains(TagReader.Open, StringComparison.Ordinal))
            {
                return;
            }

            var location = new DiagnosticLocation(label, number, joined.Length <= ExcerptLength ? joined : joined[..ExcerptLength] + "...");
            var before = diagnostics.Count;
            var tags = new TagReader(dictionary, location, diagnostics).Read(joined);
            if (IsConditionAloneOnItsLine(tags, joined))
            {
                // Alone on its line, the tag marks a boundary between paragraphs.
                Markers.Add(new ConditionMarker(paragraph, tags[0], location, Reported: diagnostics.Count > before));
                return;
            }

            Markers.AddRange(InlineConditions.Unpaired(tags, location, diagnostics)
                .Select(tag => new ConditionMarker(paragraph, tag, location, Reported: true)));
            if (diagnostics.Count > before)
            {
                return;
            }

            // Last first, so replacing one tag does not move the text before it.
            tags.AsEnumerable().Reverse().ToList().ForEach(tag => Replace(texts, tag));
            texts.Where(t => t.Value.Length == 0).ToList().ForEach(t => t.Remove());
            InlineConditions.Wrap(paragraph, location, diagnostics);
        }
    }

    /// <summary>
    /// Puts a stand-in where the tag starts, in the run that holds its first character, so a value takes that run's
    /// formatting. The tag's own characters are removed from every piece of text they were spread across.
    /// </summary>
    private static void Replace(List<XElement> texts, Tag tag)
    {
        var offset = 0;
        var placed = false;
        foreach (var text in texts)
        {
            var value = text.Value;
            var from = Math.Clamp(tag.Start - offset, 0, value.Length);
            var to = Math.Clamp(tag.End - offset, 0, value.Length);
            offset += value.Length;
            if (from == to)
            {
                continue;
            }

            if (placed)
            {
                Keep(text, value[to..]);
                continue;
            }

            placed = true;
            Keep(text, value[..from]);
            var slot = Standing(tag);
            text.AddAfterSelf(slot);
            if (to < value.Length)
            {
                slot.AddAfterSelf(Keep(new XElement(Word + "t"), value[to..]));
            }
        }
    }

    private static bool IsConditionAloneOnItsLine(List<Tag> tags, string paragraphText) =>
        tags is [{ Kind: not TagKind.Value } only] && paragraphText.Trim().Length == only.End - only.Start;

    /// <summary>The element that stands in for a tag until the template is rendered.</summary>
    private static XElement Standing(Tag tag) => tag.Kind switch
    {
        TagKind.If => new XElement(InlineConditions.Start, new XAttribute("key", tag.Field!.Key)),
        TagKind.EndIf => new XElement(InlineConditions.End),
        _ => new XElement(Value, new XAttribute("key", tag.Field!.Key)),
    };

    private static XElement Keep(XElement text, string value)
    {
        text.Value = value;
        text.SetAttributeValue(XNamespace.Xml + "space", "preserve");
        return text;
    }
}
