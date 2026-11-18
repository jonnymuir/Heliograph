using System.Text;
using System.Xml.Linq;
using Heliograph.Pdf.Reading;
using static Heliograph.Pdf.Reading.WordXml;

namespace Heliograph.Pdf.Checking;

/// <summary>
/// Fields the converter works out: the page number, and the count of pages in the letter or its section, in figures,
/// letters or Roman numerals. Each field's whole instruction is read, though Word may split it across runs.
/// </summary>
internal static class FieldLimits
{
    public static IEnumerable<(XElement, string)> Found(XElement root)
    {
        foreach (var (place, instruction) in Instructions(root))
        {
            var read = FieldInstruction.Parse(instruction);
            if (read.Name.Length > 0 && read.Name is not ("PAGE" or "NUMPAGES" or "SECTIONPAGES"))
            {
                yield return (place, $"a {read.Name} field");
            }
            else if (read.Unsupported is { } unsupported)
            {
                yield return (place, $"a {read.Name} field with the switch {unsupported}");
            }
            else if (read.Name.Length > 0 && Placed(place, read.Name) is { } where)
            {
                yield return (place, where);
            }
        }

        foreach (var number in root.Descendants(W + "pgNum").Where(e => Placed(e, "PAGE") is not null))
        {
            yield return (number, Placed(number, "PAGE")!);
        }
    }

    // Page numbers are worked out in headers, footers and the body's paragraphs; counts of pages only in headers and footers,
    // as they are known once the letter is laid out. Elsewhere, in a table or a note, neither is yet.
    private static string? Placed(XElement place, string name)
    {
        var story = place.Ancestors().Any(a => a.Name.LocalName is "hdr" or "ftr");
        if (story)
        {
            return null;
        }

        if (place.Ancestors(W + "tbl").Any() || place.Ancestors().Any(a => a.Name.LocalName is "footnotes" or "endnotes"))
        {
            return $"a {name} field in a table or a note";
        }

        return name == "PAGE" ? null : $"a {name} field in the body of the letter";
    }

    // Each field's instruction, at the place it starts: a simple field's attribute, or a complex field's instruction text
    // from its begin mark to its separate or end mark, fields inside fields each on their own.
    private static IEnumerable<(XElement Place, string Instruction)> Instructions(XElement root)
    {
        foreach (var simple in root.Descendants(W + "fldSimple"))
        {
            yield return (simple, (string?)simple.Attribute(W + "instr") ?? "");
        }

        var open = new Stack<(XElement? Place, StringBuilder Text, bool Done)>();
        foreach (var e in root.Descendants().Where(e => e.Name == W + "fldChar" || e.Name == W + "instrText"))
        {
            var type = (string?)e.Attribute(W + "fldCharType");
            if (type == "begin")
            {
                open.Push((null, new StringBuilder(), false));
            }
            else if (e.Name == W + "instrText" && open.TryPeek(out var top) && !top.Done)
            {
                open.Pop();
                open.Push((top.Place ?? e, top.Text.Append(e.Value), false));
            }
            else if (type is "separate" or "end" && open.TryPop(out var field))
            {
                if (!field.Done && field.Place is not null)
                {
                    yield return (field.Place, field.Text.ToString());
                }

                if (type == "separate")
                {
                    open.Push((field.Place, field.Text, true));
                }
            }
        }
    }
}
