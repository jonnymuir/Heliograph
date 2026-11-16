using System.Xml.Linq;

namespace Heliograph.Documents;

/// <summary>One place a template uses a field that belongs to a list.</summary>
internal sealed record ListUse(XElement Element, Field Field, Field List, DiagnosticLocation Location);

/// <summary>The item of a list that a copy of a repeated row is for.</summary>
internal sealed record ItemScope(Field List, System.Text.Json.JsonElement Item, int Number);

/// <summary>
/// A table row that uses a list's fields is repeated once for each item. There is no marker to write: the
/// dictionary already says the fields belong to the list, so the row they are in can only mean one row per item.
/// </summary>
internal static class Repeats
{
    /// <summary>The element a compiled template holds round a row that repeats.</summary>
    public static readonly XName Each = WordXml.Slot + "each";

    private static readonly XNamespace Word = WordXml.Word;

    /// <summary>Finds the row each use is in, checks that the row can repeat, and wraps it.</summary>
    public static void Mark(IReadOnlyList<ListUse> uses, List<Diagnostic> diagnostics)
    {
        var rows = new Dictionary<XElement, ListUse>();
        foreach (var use in uses)
        {
            if (Place(use, rows) is { } problem)
            {
                diagnostics.Add(problem);
            }
        }

        foreach (var (row, use) in rows)
        {
            if (WhyNot(row) is { } reason)
            {
                diagnostics.Add(new Diagnostic(DiagnosticCodes.RowCannotRepeat,
                    $"This table row would repeat for each item in \"{use.List.Name}\", but {reason}.",
                    "Use an ordinary row for the list's fields.", use.Location));
            }
            else
            {
                Wrap(row, use.List);
            }
        }
    }

    /// <summary>Records the row a use is in, or says why the use has no row it can repeat.</summary>
    private static Diagnostic? Place(ListUse use, Dictionary<XElement, ListUse> rows)
    {
        if (use.Element.Ancestors(Word + "tr").FirstOrDefault() is not { } row)
        {
            return new Diagnostic(DiagnosticCodes.ListFieldOutsideRow,
                $"\"{use.Field.Name}\" is part of the list \"{use.List.Name}\", so it can only be used in a row of a table.",
                "Put it in a table row. The row is repeated for each item in the list.", use.Location);
        }

        if (rows.TryAdd(row, use) || rows[row].List == use.List)
        {
            return null;
        }

        return new Diagnostic(DiagnosticCodes.RowMixesLists,
            $"This table row uses fields from two lists, \"{rows[row].List.Name}\" and \"{use.List.Name}\". A row can repeat for one list only.",
            "Put the fields of each list in a row, or a table, of their own.", use.Location);
    }

    private static void Wrap(XElement row, Field list)
    {
        // A bookmark names one place. Copied into every row it would name many, so it is dropped.
        row.Descendants().Where(e => e.Name == Word + "bookmarkStart" || e.Name == Word + "bookmarkEnd").ToList().ForEach(e => e.Remove());
        var each = new XElement(Each, new XAttribute("list", list.Key));
        row.AddBeforeSelf(each);
        row.Remove();
        each.Add(row);
    }

    /// <summary>
    /// Replaces each repeating row with one copy for each item. A copy remembers its item, so the values and
    /// conditions inside it are read from that item.
    /// </summary>
    public static void Expand(XDocument part, Func<string, Field> list, RecipientData recipient)
    {
        var repeats = part.Descendants(Each).ToList();
        foreach (var each in repeats)
        {
            var field = list((string)each.Attribute("list")!);
            var row = each.Elements().Single();
            var items = recipient.Items(field) ?? [];
            each.ReplaceWith(items.Select((item, index) =>
            {
                var copy = new XElement(row);
                copy.AddAnnotation(new ItemScope(field, item, index + 1));
                return copy;
            }));
        }

        if (repeats.Count > 0)
        {
            NumberDrawings(part);
        }
    }

    /// <summary>Why a row cannot be one that repeats, or null if it can.</summary>
    private static string? WhyNot(XElement row)
    {
        if (row.Element(Word + "trPr")?.Element(Word + "tblHeader") is not null)
        {
            return "it is marked as a heading row, which is printed again at the top of each page";
        }

        if (row.Elements(Word + "tc").Any(cell => cell.Element(Word + "tcPr")?.Element(Word + "vMerge") is not null))
        {
            return "one of its cells is merged with a cell in the row above or below";
        }

        return row.Descendants().Any(e => e.Name == Word + "footnoteReference" || e.Name == Word + "endnoteReference")
            ? "it holds a footnote or an endnote, which can be referred to once only"
            : null;
    }

    /// <summary>Every picture and shape in a part needs a number of its own, and a copied row copies the number.</summary>
    private static void NumberDrawings(XDocument part)
    {
        var drawings = part.Descendants().Where(e => e.Name.LocalName == "docPr" && e.Attribute("id") is not null).ToList();
        if (drawings.Select(drawing => (string)drawing.Attribute("id")!).Distinct().Count() == drawings.Count)
        {
            return;
        }

        for (var index = 0; index < drawings.Count; index++)
        {
            drawings[index].SetAttributeValue("id", index + 1);
        }
    }
}
