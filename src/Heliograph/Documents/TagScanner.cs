using System.Xml.Linq;

namespace Heliograph.Documents;

/// <summary>
/// Finds each tag, ((Field name)), in a part and swaps it for a slot the renderer fills. An editor may store one
/// tag in several pieces of text, so a paragraph's text is joined before it is read.
/// </summary>
internal static class TagScanner
{
    public static readonly XNamespace Word = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";

    /// <summary>The namespace of a slot. Private to a compiled template: it never appears in a file.</summary>
    public static readonly XNamespace Slot = "urn:heliograph:compiled";

    private const string Open = "((";
    private const string Close = "))";
    private const int ExcerptLength = 80;

    public static void Compile(XDocument part, string label, FieldDictionary dictionary, List<Diagnostic> diagnostics)
    {
        // An uploaded file must not be able to plant a slot of its own.
        part.Descendants().Where(e => e.Name.Namespace == Slot).ToList().ForEach(e => e.Remove());

        var paragraphs = part.Descendants(Word + "p").ToList();
        for (var index = 0; index < paragraphs.Count; index++)
        {
            var paragraph = paragraphs[index];

            // A text box sits inside a paragraph and has paragraphs of its own, which get their own turn.
            var texts = paragraph.Descendants(Word + "t").Where(t => t.Ancestors(Word + "p").First() == paragraph).ToList();
            var joined = string.Concat(texts.Select(t => t.Value));
            if (!joined.Contains(Open, StringComparison.Ordinal))
            {
                continue;
            }

            var location = new DiagnosticLocation(label, index + 1,
                joined.Length <= ExcerptLength ? joined : joined[..ExcerptLength] + "...");
            var before = diagnostics.Count;
            var tags = Find(joined, dictionary, location, diagnostics);
            if (diagnostics.Count > before)
            {
                continue;
            }

            // Last first, so replacing one tag does not move the text before it.
            foreach (var tag in tags.AsEnumerable().Reverse())
            {
                Replace(texts, tag);
            }

            texts.Where(t => t.Value.Length == 0).ToList().ForEach(t => t.Remove());
        }
    }

    private static List<(int Start, int End, Field Field)> Find(
        string text, FieldDictionary dictionary, DiagnosticLocation location, List<Diagnostic> diagnostics)
    {
        var tags = new List<(int, int, Field)>();
        var position = 0;
        while (text.IndexOf(Open, position, StringComparison.Ordinal) is var open and >= 0)
        {
            var close = text.IndexOf(Close, open + Open.Length, StringComparison.Ordinal);
            if (close < 0)
            {
                diagnostics.Add(new Diagnostic(DiagnosticCodes.TagNotClosed,
                    "A tag is opened with (( but never closed.",
                    "Add )) after the field's name, in the same paragraph.", location));
                break;
            }

            var name = text[(open + Open.Length)..close].Trim();
            position = close + Close.Length;
            if (name.Length == 0)
            {
                diagnostics.Add(new Diagnostic(DiagnosticCodes.TagHasNoName,
                    "A tag has nothing between (( and )).",
                    "Put the name of a field from the dictionary between the brackets.", location));
            }
            else if (dictionary.Find(name) is { } field)
            {
                tags.Add((open, position, field));
            }
            else
            {
                var nearest = FieldName.Nearest(name, dictionary.Fields.Select(e => e.Name));
                diagnostics.Add(new Diagnostic(DiagnosticCodes.UnknownField,
                    $"The dictionary has no field called \"{name}\".",
                    nearest is null ? "Use the name of a field from the dictionary." : $"Did you mean (({nearest}))?",
                    location));
            }
        }

        return tags;
    }

    /// <summary>
    /// Puts a slot where the tag starts, in the run that holds its first character, so the value takes that run's
    /// formatting. The tag's own characters are removed from every piece of text they were spread across.
    /// </summary>
    private static void Replace(List<XElement> texts, (int Start, int End, Field Field) tag)
    {
        var offset = 0;
        XElement? first = null;
        foreach (var text in texts)
        {
            var length = text.Value.Length;
            var from = Math.Clamp(tag.Start - offset, 0, length);
            var to = Math.Clamp(tag.End - offset, 0, length);
            var value = text.Value;
            offset += length;
            if (from == to)
            {
                continue;
            }

            if (first is null)
            {
                first = text;
                Keep(text, value[..from]);
                var slot = new XElement(Slot + "value", new XAttribute("key", tag.Field.Key));
                text.AddAfterSelf(slot);
                if (to < length)
                {
                    slot.AddAfterSelf(Keep(new XElement(Word + "t"), value[to..]));
                }
            }
            else
            {
                Keep(text, value[to..]);
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
