using System.Xml.Linq;

namespace Heliograph.Documents;

/// <summary>
/// At render, keeps or removes the content each condition governs, and leaves the document valid afterwards.
/// </summary>
internal static class ConditionResolver
{
    private static readonly XNamespace Word = WordXml.Word;

    // What a paragraph may still hold and count as empty: runs, their formatting, and the invisible marks editors leave.
    private static readonly HashSet<string> Invisible = ["r", "rPr", "proofErr", "bookmarkStart", "bookmarkEnd"];

    /// <summary>
    /// Works outermost first. It runs before any value is filled in, so a field used only in removed content needs
    /// no value.
    /// </summary>
    public static void Resolve(XDocument part, Func<XElement, bool> isYes, bool insideRepeatedRows)
    {
        var sentences = new HashSet<XElement>();
        // A condition in a row that repeats is decided for each item, so it waits until the row has been copied.
        bool Ready(XElement condition) => insideRepeatedRows || !condition.Ancestors(Repeats.Each).Any();
        while (part.Descendants(Conditions.Element).FirstOrDefault(Ready) is { } condition)
        {
            if (isYes(condition))
            {
                condition.ReplaceWith(condition.Nodes());
                continue;
            }

            if (condition.Attribute("inline") is not null && condition.Ancestors(Word + "p").FirstOrDefault() is { } paragraph)
            {
                sentences.Add(paragraph);
            }

            condition.Remove();
        }

        // Judged once every condition is settled, so the answer does not depend on the order they came in. A
        // sentence that lost a condition and now shows nothing leaves no paragraph behind.
        foreach (var paragraph in sentences.Where(paragraph => paragraph.Parent is not null && IsEmpty(paragraph)))
        {
            paragraph.Remove();
        }

    }

    /// <summary>Whether a paragraph shows nothing: no text, no picture, no break, and no section break to carry.</summary>
    private static bool IsEmpty(XElement paragraph)
    {
        var properties = paragraph.Element(Word + "pPr");
        var carriesSectionBreak = properties?.Element(Word + "sectPr") is not null;
        return !carriesSectionBreak && paragraph.Elements().Where(e => e != properties).All(ShowsNothing);
    }

    private static bool ShowsNothing(XElement element) => element.DescendantsAndSelf()
        .Where(e => !e.AncestorsAndSelf().Any(a => a.Name == Word + "rPr"))
        .All(e => Invisible.Contains(e.Name.LocalName) || (e.Name == Word + "t" && string.IsNullOrWhiteSpace(e.Value)));
}
