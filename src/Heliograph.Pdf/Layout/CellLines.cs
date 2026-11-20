using Heliograph.Pdf.Reading;

namespace Heliograph.Pdf.Layout;

/// <summary>
/// A cell's text broken into lines down it, each at its top: its paragraphs, each tagged as a paragraph in the cell, and a
/// table inside it as one line of its own that does not break.
/// </summary>
internal sealed class CellLines(double width, StructNode? cell)
{
    private readonly List<(double Top, Line Line, Owner Owner)> _lines = [];
    private double _y;

    public static (List<(double Top, Line Line, Owner Owner)> Lines, double Height) Of(IReadOnlyList<Block> blocks, double width, StructNode? cell)
    {
        var lines = new CellLines(width, cell);
        foreach (var block in blocks.Where((b, i) => !EndAfterTable(blocks, i)))
        {
            lines.Add(block);
        }

        return (lines._lines, lines._y);
    }

    // The empty paragraph Word requires after a table at the end of a cell takes no room, as Word and LibreOffice lay it out.
    private static bool EndAfterTable(IReadOnlyList<Block> blocks, int i) =>
        i == blocks.Count - 1 && i > 0 && blocks[i] is Paragraph p && p.Inlines.All(x => x is TextInline { Text.Length: 0 }) && blocks[i - 1] is Table;

    private void Add(Block block)
    {
        if (block is Table inner)
        {
            var nested = NestedTable.Lay(inner, width, cell?.Add("Table"));
            _lines.Add((_y, nested, Owner.Artifact));
            _y += nested.Height;
        }
        else if (block is Paragraph p)
        {
            Paragraph(p);
        }
    }

    private void Paragraph(Paragraph p)
    {
        _y += p.Style.SpaceAbove;
        var owner = cell is null ? Owner.Artifact : new Owner(cell.Add("P"), null);
        foreach (var line in LineBreaker.Break(p, width, FieldValues.Of))
        {
            _lines.Add((_y, line, owner));
            _y += line.Height;
        }

        _y += p.Style.Spacing.After;
    }
}
