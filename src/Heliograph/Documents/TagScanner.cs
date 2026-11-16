using System.Xml.Linq;

namespace Heliograph.Documents;

/// <summary>
/// Finds each tag in a part and swaps it for something the renderer acts on. An editor may store one tag in several
/// pieces of text, so a paragraph's text is joined before it is read.
/// </summary>
internal static class TagScanner
{
    public static readonly XNamespace Word = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";

    /// <summary>The namespace of what a compiled template holds in place of tags. It never appears in a file.</summary>
    public static readonly XNamespace Slot = "urn:heliograph:compiled";

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
            if (tags.Any(tag => tag.Kind != TagKind.Value))
            {
                Condition(paragraph, tags, joined, location);
            }
            else if (diagnostics.Count == before)
            {
                // Last first, so replacing one tag does not move the text before it.
                tags.AsEnumerable().Reverse().ToList().ForEach(tag => Replace(texts, tag));
                texts.Where(t => t.Value.Length == 0).ToList().ForEach(t => t.Remove());
            }
        }

        /// <summary>An ((if ...)) or ((end if)) must be the only thing in its paragraph, which then marks a boundary.</summary>
        private void Condition(XElement paragraph, List<Tag> tags, string joined, DiagnosticLocation location)
        {
            if (tags.Count == 1 && joined.Trim().Length == tags[0].End - tags[0].Start)
            {
                Markers.Add(new ConditionMarker(paragraph, tags[0], location));
                return;
            }

            diagnostics.Add(new Diagnostic(DiagnosticCodes.ConditionTagNotAlone,
                "An ((if ...)) or ((end if)) tag shares its paragraph with other text or tags.",
                "Put each ((if ...)) and ((end if)) on a line of its own, with nothing else in that paragraph.", location));
        }
    }

    /// <summary>
    /// Puts a slot where the tag starts, in the run that holds its first character, so the value takes that run's
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
            var slot = new XElement(Slot + "value", new XAttribute("key", tag.Field!.Key));
            text.AddAfterSelf(slot);
            if (to < value.Length)
            {
                slot.AddAfterSelf(Keep(new XElement(Word + "t"), value[to..]));
            }
        }
    }

    private static XElement Keep(XElement text, string value)
    {
        text.Value = value;
        text.SetAttributeValue(XNamespace.Xml + "space", "preserve");
        return text;
    }
}
