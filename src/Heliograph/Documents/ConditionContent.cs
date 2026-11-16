using System.Xml.Linq;

namespace Heliograph.Documents;

/// <summary>
/// Checks what a condition governs before it is accepted. Hiding it must not cut through anything that has a start
/// and an end of its own, or take away something the rest of the document depends on.
/// </summary>
internal static class ConditionContent
{
    private static readonly XNamespace Word = WordXml.Word;

    /// <summary>What is wrong with governing this content, if anything.</summary>
    public static Diagnostic? Problem(IReadOnlyList<XNode> content, string on, DiagnosticLocation location)
    {
        var elements = content.OfType<XElement>().SelectMany(e => e.DescendantsAndSelf()).ToList();
        if (elements.Any(e => e.Name == Word + "sectPr"))
        {
            return SectionBreak($"The condition{on} contains a section break.", location);
        }

        // A field is a begin mark, its code, and an end mark, spread over several runs and sometimes paragraphs.
        var depth = 0;
        foreach (var type in elements.Where(e => e.Name == Word + "fldChar").Select(e => (string?)e.Attribute(Word + "fldCharType")))
        {
            depth += type == "begin" ? 1 : type == "end" ? -1 : 0;
            if (depth < 0)
            {
                break;
            }
        }

        return depth == 0
            ? null
            : new Diagnostic(DiagnosticCodes.ConditionCutsThroughField,
                $"The condition{on} starts or ends in the middle of a field, such as a page number, a cross-reference or a table of contents.",
                "Move ((if ...)) and ((end if)) so that the whole field is inside the condition, or the whole field is outside it.",
                location);
    }

    /// <summary>A tag line is removed when the template is compiled, and a section break on it would go with it.</summary>
    public static Diagnostic? TagLineProblem(ConditionMarker marker) =>
        marker.Paragraph.Element(Word + "pPr")?.Element(Word + "sectPr") is null
            ? null
            : SectionBreak("This condition tag is on the last line of a section, which carries the section break.", marker.Location);

    private static Diagnostic SectionBreak(string message, DiagnosticLocation location) => new(DiagnosticCodes.ConditionHoldsSectionBreak,
        message + " Hiding it would merge two sections, changing page size, margins, headers and footers.",
        "Move the section break outside the condition, or move the tags so that the section break is not between them or on their lines.",
        location);
}
