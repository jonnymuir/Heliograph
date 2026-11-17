using Heliograph.Pdf.Layout;
using static Heliograph.Pdf.Writing.PdfObjects;

namespace Heliograph.Pdf.Writing;

/// <summary>
/// Writes the structure tree: an element for each node, with its parent, its kids and the page each piece of its text is
/// on, and the parent tree a reader uses to go from marked text back to its element.
/// </summary>
internal static class StructureWriter
{
    public static void Write(PdfObjects objects, int structRoot, StructNode root, IReadOnlyList<int> pageIds, IReadOnlyList<List<StructNode>> marked)
    {
        Prune(root);
        var ids = new Dictionary<StructNode, int>();
        Assign(root, ids, objects);
        foreach (var (node, id) in ids)
        {
            objects.Set(id, Element(node, ids, pageIds, structRoot));
        }

        var nums = string.Join(" ", marked.Select((list, n) => Invariant($"{n} [{string.Join(" ", list.Select(node => Invariant($"{ids[node]} 0 R")))}]")));
        objects.Set(structRoot, Invariant($"<</Type /StructTreeRoot /K [{ids[root]} 0 R] /ParentTree <</Nums [{nums}]>> /ParentTreeNextKey {pageIds.Count}>>"));
    }

    private static void Assign(StructNode node, Dictionary<StructNode, int> ids, PdfObjects objects)
    {
        ids[node] = objects.Reserve();
        foreach (var kid in node.Kids.OfType<StructNode>())
        {
            Assign(kid, ids, objects);
        }
    }

    private static string Element(StructNode node, Dictionary<StructNode, int> ids, IReadOnlyList<int> pageIds, int structRoot)
    {
        var kids = string.Join(" ", node.Kids.Select(k => k switch
        {
            StructNode child => Invariant($"{ids[child]} 0 R"),
            MarkedRef m => Invariant($"<</Type /MCR /Pg {pageIds[m.Page]} 0 R /MCID {m.Mcid}>>"),
            _ => "",
        }));
        var parent = node.Parent is { } p ? ids[p] : structRoot;
        var table = (node.Scope is { } scope ? $" /Scope /{scope}" : "") + (node.ColumnSpan > 1 ? Invariant($" /ColSpan {node.ColumnSpan}") : "");
        var attributes = table.Length > 0 ? $" /A <</O /Table{table}>>" : "";
        return Invariant($"<</Type /StructElem /S /{node.Type} /P {parent} 0 R /K [{kids}]{attributes}>>");
    }

    // An element with nothing in it would only confuse a screen reader; a table's empty cells and rows still hold their place.
    private static bool Prune(StructNode node)
    {
        node.Kids.RemoveAll(k => k is StructNode child && Prune(child));
        return node.Kids.Count == 0 && node.Type is not ("Document" or "Table" or "TR" or "TH" or "TD");
    }
}
