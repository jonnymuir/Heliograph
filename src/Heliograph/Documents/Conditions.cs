using System.Xml.Linq;

namespace Heliograph.Documents;

/// <summary>
/// A condition tag that stands for a boundary between paragraphs: one alone on its line, or one inside a sentence
/// that found no partner there. <paramref name="Reported"/> is true when it has already been reported as wrong.
/// </summary>
internal sealed record ConditionMarker(XElement Paragraph, Tag Tag, DiagnosticLocation Location, bool Reported);

/// <summary>
/// Pairs each ((if Field)) on a line of its own with its ((end if)) and wraps what lies between them, so the
/// renderer can keep or remove it in one move. The two tag paragraphs go, which is why a hidden section leaves no gap.
/// </summary>
internal static class Conditions
{
    /// <summary>The element a compiled template holds in place of a condition. Its children are the governed content.</summary>
    public static readonly XName Element = WordXml.Slot + "if";

    public static void Wrap(IEnumerable<ConditionMarker> markers, List<Diagnostic> diagnostics)
    {
        var pairs = Pair(markers, diagnostics);
        diagnostics.AddRange(pairs.SelectMany(Problems));
        if (diagnostics.Errors() > 0)
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

    /// <summary>What is wrong with a matched pair: with either tag line, or with what lies between them.</summary>
    private static IEnumerable<Diagnostic> Problems((ConditionMarker Start, ConditionMarker End) pair)
    {
        var between = pair.Start.Paragraph.NodesAfterSelf().TakeWhile(node => node != pair.End.Paragraph).ToList();
        Diagnostic?[] found =
        [
            ConditionContent.TagLineProblem(pair.Start),
            ConditionContent.TagLineProblem(pair.End),
            ConditionContent.Problem(between, On(pair.Start.Tag), pair.Start.Location),
        ];
        return found.OfType<Diagnostic>().Take(1);
    }

    /// <summary>Matches each end to the condition it closes, and reports every tag left without a partner.</summary>
    private static List<(ConditionMarker Start, ConditionMarker End)> Pair(IEnumerable<ConditionMarker> markers, List<Diagnostic> diagnostics)
    {
        var open = new Stack<ConditionMarker>();
        var pairs = new List<(ConditionMarker, ConditionMarker)>();
        foreach (var marker in markers)
        {
            if (marker.Tag.Kind == TagKind.If)
            {
                open.Push(marker);
            }
            else if (!open.TryPop(out var start))
            {
                Report(marker, diagnostics, new Diagnostic(DiagnosticCodes.EndWithoutCondition,
                    "This ((end if)) has no ((if ...)) before it to end.",
                    "Remove it, or add the ((if ...)) tag it belongs to on a line of its own above.", marker.Location));
            }
            else if (start.Paragraph.Parent != marker.Paragraph.Parent)
            {
                Report(start.Reported ? start : marker, diagnostics, NotClosed(start,
                    "Its ((end if)) is in a different place, such as outside the table cell the condition starts in."));
            }
            else
            {
                pairs.Add((start, marker));
            }
        }

        foreach (var start in open.Reverse())
        {
            Report(start, diagnostics, NotClosed(start, "It has no ((end if)) after it."));
        }

        return pairs;
    }

    /// <summary>" on "Field name"", or nothing when the tag's field was not understood.</summary>
    public static string On(Tag tag) => tag.Field is { } field ? $" on \"{field.Name}\"" : "";

    private static void Report(ConditionMarker marker, List<Diagnostic> diagnostics, Diagnostic diagnostic)
    {
        if (!marker.Reported)
        {
            diagnostics.Add(diagnostic);
        }
    }

    private static Diagnostic NotClosed(ConditionMarker start, string why) => new(DiagnosticCodes.ConditionNotClosed,
        $"The condition{On(start.Tag)} is not closed. {why}",
        "Put ((end if)) on a line of its own after the content the condition governs, in the same part of the document.",
        start.Location);
}
