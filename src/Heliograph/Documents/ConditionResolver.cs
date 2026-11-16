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
    public static void Resolve(XDocument part, Func<string, bool> isYes)
    {
        while (part.Descendants(Conditions.Element).FirstOrDefault() is { } condition)
        {
            if (isYes((string)condition.Attribute("key")!))
            {
                condition.ReplaceWith(condition.Nodes());
            }
            else
            {
                Hide(condition);
            }
        }

        // A table cell must end with a paragraph, even when everything in it was hidden.
        var emptied = part.Descendants(Word + "tc").Where(cell => cell.Elements().LastOrDefault()?.Name != Word + "p").ToList();
        emptied.ForEach(cell => cell.Add(new XElement(Word + "p")));
    }

    /// <summary>Removes a condition and its content. If that was all a sentence held, its paragraph goes too.</summary>
    private static void Hide(XElement condition)
    {
        var paragraph = condition.Attribute("inline") is null ? null : condition.Ancestors(Word + "p").FirstOrDefault();
        condition.Remove();
        if (paragraph is not null && IsEmpty(paragraph))
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
