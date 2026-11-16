using System.Xml.Linq;

namespace Heliograph.Documents;

/// <summary>
/// A condition inside a sentence: ((if Field)) and ((end if)) in the same paragraph, around part of its text.
/// </summary>
internal static class InlineConditions
{
    public static readonly XName Start = WordXml.Slot + "ifStart";
    public static readonly XName End = WordXml.Slot + "ifEnd";

    /// <summary>
    /// Checks that the condition tags in one paragraph pair up, and reports each one that does not. The unpaired
    /// tags are returned, so that a tag on a line of its own elsewhere can still pair with them without a second report.
    /// </summary>
    public static List<Tag> Unpaired(IEnumerable<Tag> tags, DiagnosticLocation location, List<Diagnostic> diagnostics)
    {
        var unpaired = new List<Tag>();
        var open = new Stack<Tag>();
        foreach (var tag in tags.Where(tag => tag.Kind != TagKind.Value))
        {
            if (tag.Kind == TagKind.If)
            {
                open.Push(tag);
            }
            else if (!open.TryPop(out _))
            {
                unpaired.Add(tag);
                diagnostics.Add(new Diagnostic(DiagnosticCodes.EndWithoutCondition,
                    "This ((end if)) is part-way through a paragraph, and no ((if ...)) is open in that paragraph.",
                    "Put the ((if ...)) it ends in the same paragraph, or put both tags on lines of their own.", location));
            }
        }

        foreach (var tag in open.Reverse())
        {
            unpaired.Add(tag);
            diagnostics.Add(new Diagnostic(DiagnosticCodes.ConditionNotClosed,
                $"The condition{Conditions.On(tag)} starts part-way through a paragraph and is not closed in it.",
                "Add ((end if)) in the same paragraph, or put ((if ...)) and ((end if)) each on a line of their own.",
                location));
        }

        return unpaired;
    }

    /// <summary>
    /// Wraps what lies between each start and end marker in a paragraph. A marker sits inside a run, so the run is
    /// split around it first, leaving each piece with the run's formatting.
    /// </summary>
    public static void Wrap(XElement paragraph, DiagnosticLocation location, List<Diagnostic> diagnostics)
    {
        var markers = paragraph.Descendants().Where(e => e.Name == Start || e.Name == End).ToList();
        markers.ForEach(Hoist);

        var open = new Stack<XElement>();
        foreach (var marker in markers)
        {
            if (marker.Name == Start)
            {
                open.Push(marker);
                continue;
            }

            var start = open.Pop();
            if (start.Parent != marker.Parent)
            {
                diagnostics.Add(new Diagnostic(DiagnosticCodes.ConditionNotClosed,
                    "A condition starts and ends in different parts of the sentence, such as inside and outside a link.",
                    "Move ((if ...)) and ((end if)) so that both are inside the link, or both outside it.", location));
                continue;
            }

            var between = start.NodesAfterSelf().TakeWhile(node => node != marker).ToList();
            if (ConditionContent.Problem(between, "", location) is { } problem)
            {
                diagnostics.Add(problem);
                continue;
            }

            between.ForEach(node => node.Remove());
            start.ReplaceWith(new XElement(Conditions.Element, start.Attribute("key"), new XAttribute("inline", true), between));
            marker.Remove();
        }
    }

    /// <summary>Moves a marker out of its run to sit between two runs: what came before it, and what came after.</summary>
    private static void Hoist(XElement marker)
    {
        var run = marker.Parent!;
        if (run.Name != WordXml.Word + "r")
        {
            return;
        }

        var after = WordXml.LikeRun(run);
        WordXml.Move(marker.NodesAfterSelf(), after);
        marker.Remove();
        run.AddAfterSelf(marker);
        if (WordXml.HasContent(after))
        {
            marker.AddAfterSelf(after);
        }

        if (!WordXml.HasContent(run))
        {
            run.Remove();
        }
    }
}
