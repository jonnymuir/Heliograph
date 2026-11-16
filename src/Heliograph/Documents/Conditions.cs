using System.Xml.Linq;

namespace Heliograph.Documents;

/// <summary>A paragraph that holds nothing but ((if Field)) or ((end if)).</summary>
internal sealed record ConditionMarker(XElement Paragraph, Tag Tag, DiagnosticLocation Location);

/// <summary>
/// Pairs each ((if Field)) with its ((end if)) and wraps what lies between them, so the renderer can keep or remove
/// it in one move. The two marker paragraphs go, which is why a hidden section leaves no gap behind.
/// </summary>
internal static class Conditions
{
    /// <summary>The element a compiled template holds in place of a condition. Its children are the governed content.</summary>
    public static readonly XName Element = TagScanner.Slot + "if";

    public static void Wrap(IEnumerable<ConditionMarker> markers, List<Diagnostic> diagnostics)
    {
        var open = new Stack<ConditionMarker>();
        var pairs = new List<(ConditionMarker Start, ConditionMarker End)>();
        foreach (var marker in markers)
        {
            if (marker.Tag.Kind == TagKind.If)
            {
                open.Push(marker);
            }
            else if (open.Count == 0)
            {
                diagnostics.Add(new Diagnostic(DiagnosticCodes.EndWithoutCondition,
                    "This ((end if)) has no ((if ...)) before it to end.",
                    "Remove it, or add the ((if ...)) tag it belongs to on a line of its own above.", marker.Location));
            }
            else if (open.Pop() is var start && start.Paragraph.Parent != marker.Paragraph.Parent)
            {
                diagnostics.Add(NotClosed(start,
                    "Its ((end if)) is in a different place, such as outside the table cell the condition starts in."));
            }
            else
            {
                pairs.Add((start, marker));
            }
        }

        diagnostics.AddRange(open.Reverse().Select(start => NotClosed(start, "It has no ((end if)) after it.")));

        if (diagnostics.Count > 0)
        {
            // The template will not compile, and a condition already reported as wrong has no field to wrap on.
            return;
        }

        // An inner pair always closes before the pair around it, so wrapping in this order nests them correctly.
        foreach (var (start, end) in pairs)
        {
            var between = start.Paragraph.NodesAfterSelf().TakeWhile(node => node != end.Paragraph).ToList();
            between.ForEach(node => node.Remove());
            start.Paragraph.ReplaceWith(new XElement(Element, new XAttribute("key", start.Tag.Field!.Key), between));
            end.Paragraph.Remove();
        }
    }

    private static Diagnostic NotClosed(ConditionMarker start, string why) => new(DiagnosticCodes.ConditionNotClosed,
        $"The condition{(start.Tag.Field is { } field ? $" on \"{field.Name}\"" : "")} is not closed. {why}",
        "Put ((end if)) on a line of its own after the content the condition governs, in the same part of the document.",
        start.Location);
}
