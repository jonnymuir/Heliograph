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

    public static void Compile(XDocument part, PartScanSettings settings, List<Diagnostic> diagnostics)
    {
        // An uploaded file must not be able to plant a slot of its own.
        part.Descendants().Where(e => e.Name.Namespace == Slot).ToList().ForEach(e => e.Remove());

        var scan = new PartScan(settings, diagnostics);
        var paragraphs = part.Descendants(Word + "p").ToList();
        for (var index = 0; index < paragraphs.Count; index++)
        {
            scan.Paragraph(paragraphs[index], index + 1);
        }

        // Rows first: a condition's tag lines are replaced when it is wrapped, and a row is found from them.
        Repeats.Mark(scan.ListUses, diagnostics);
        Conditions.Wrap(scan.Markers, diagnostics);
    }

    /// <summary>The scan of one part: what it is checked against, and what it has found so far.</summary>
    private sealed class PartScan(PartScanSettings settings, List<Diagnostic> diagnostics)
    {
        private bool _hiddenTextReported;

        public List<ConditionMarker> Markers { get; } = [];

        /// <summary>Every place a field that belongs to a list is used, so the row it is in can be made to repeat.</summary>
        public List<ListUse> ListUses { get; } = [];

        public void Paragraph(XElement paragraph, int number)
        {
            // A text box sits inside a paragraph and has paragraphs of its own, which get their own turn.
            var texts = paragraph.Descendants(Word + "t").Where(t => t.Ancestors(Word + "p").First() == paragraph).ToList();
            var joined = string.Concat(texts.Select(t => t.Value));
            var location = new DiagnosticLocation(settings.Label, number, joined.Length <= ExcerptLength ? joined : joined[..ExcerptLength] + "...");
            HiddenText(texts, location);
            if (!joined.Contains(TagReader.Open, StringComparison.Ordinal))
            {
                LooksLikeATag(joined, location);
                return;
            }

            var before = diagnostics.Errors();
            var tags = new TagReader(settings.Dictionary, location, diagnostics).Read(joined);
            if (IsConditionAloneOnItsLine(tags, joined))
            {
                // Alone on its line, the tag marks a boundary between paragraphs.
                Markers.Add(new ConditionMarker(paragraph, tags[0], ListOf(tags[0]), location, Reported: diagnostics.Errors() > before));
                NoteListUse(paragraph, tags[0], location);
                return;
            }

            Markers.AddRange(InlineConditions.Unpaired(tags, location, diagnostics)
                .Select(tag => new ConditionMarker(paragraph, tag, ListOf(tag), location, Reported: true)));
            if (diagnostics.Errors() > before)
            {
                return;
            }

            LooksLikeATag(Without(joined, tags), location);

            // Last first, so replacing one tag does not move the text before it.
            foreach (var tag in tags.AsEnumerable().Reverse())
            {
                Replace(texts, tag, Standing(tag, ListOf(tag)));

                // Noted against the paragraph, which stays where it is. A stand-in may be moved when a condition is wrapped.
                NoteListUse(paragraph, tag, location);
            }

            texts.Where(t => t.Value.Length == 0).ToList().ForEach(t => t.Remove());
            InlineConditions.Wrap(paragraph, location, diagnostics);
        }

        private Field? ListOf(Tag tag) => tag.Field is null ? null : settings.Dictionary.ListOf(tag.Field);

        private void NoteListUse(XElement element, Tag tag, DiagnosticLocation location)
        {
            if (ListOf(tag) is { } list)
            {
                ListUses.Add(new ListUse(element, tag.Field!, list, location));
            }
        }

        private void LooksLikeATag(string text, DiagnosticLocation location)
        {
            if (settings.NearMiss.In(text) is { } field)
            {
                diagnostics.Add(new Diagnostic(DiagnosticCodes.LooksLikeATag,
                    $"This looks like a tag for \"{field.Name}\", but it is not written as one, so it will be printed as it is.",
                    $"If it is meant to be filled in, write it as (({field.Name})), with two round brackets each side. "
                    + "If it is ordinary text, nothing needs to change.",
                    location, DiagnosticSeverity.Warning));
            }
        }

        /// <summary>Reported once for each part, at the first place it is found.</summary>
        private void HiddenText(List<XElement> texts, DiagnosticLocation location)
        {
            if (_hiddenTextReported || !texts.Any(IsHidden))
            {
                return;
            }

            _hiddenTextReported = true;
            diagnostics.Add(new Diagnostic(DiagnosticCodes.HasHiddenText,
                "This document has hidden text. It is sent to every recipient inside the file, where it can be revealed.",
                "Delete the hidden text if recipients should not have it. If they may, nothing needs to change.",
                location, DiagnosticSeverity.Warning));
        }

        private static bool IsHidden(XElement text)
        {
            var hidden = text.Parent?.Element(Word + "rPr")?.Element(Word + "vanish");
            var off = (string?)hidden?.Attribute(Word + "val") is "0" or "false";
            return hidden is not null && !off && !string.IsNullOrWhiteSpace(text.Value);
        }

        /// <summary>The paragraph's text with each recognised tag blanked out, leaving what will be printed as it is.</summary>
        private static string Without(string text, List<Tag> tags)
        {
            var characters = text.ToCharArray();
            foreach (var tag in tags)
            {
                Array.Fill(characters, ' ', tag.Start, tag.End - tag.Start);
            }

            return new string(characters);
        }
    }

    /// <summary>
    /// Puts a stand-in where the tag starts, in the run that holds its first character, so a value takes that run's
    /// formatting. The tag's own characters are removed from every piece of text they were spread across.
    /// </summary>
    private static void Replace(List<XElement> texts, Tag tag, XElement standIn)
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
            var slot = standIn;
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
    private static XElement Standing(Tag tag, Field? list) => tag.Kind switch
    {
        TagKind.If => new XElement(InlineConditions.Start, WordXml.FieldAttributes(tag.Field!, list)),
        TagKind.EndIf => new XElement(InlineConditions.End),
        _ => new XElement(Value, WordXml.FieldAttributes(tag.Field!, list)),
    };

    private static XElement Keep(XElement text, string value)
    {
        text.Value = value;
        text.SetAttributeValue(XNamespace.Xml + "space", "preserve");
        return text;
    }
}

/// <summary>What one part is scanned with: its name in diagnostics, the dictionary, and the near-miss finder built from it.</summary>
internal sealed record PartScanSettings(string Label, FieldDictionary Dictionary, NearMiss NearMiss);
