using Heliograph.Pdf.Reading;

namespace Heliograph.Pdf.Layout;

/// <summary>
/// A table inside a table's cell, laid out on a canvas of its own as wide as the cell's text: each row whole, below the one
/// before, and the table's bottom border below its last. It is then set in the cell as one line that does not break.
/// </summary>
internal static class NestedTable
{
    public static Line Lay(Table t, double width, StructNode? node)
    {
        var canvas = new LaidOutPage();
        var y = 0.0;
        RowLayout? last = null;
        foreach (var row in t.Rows)
        {
            last = RowLayout.Of(t, row, node?.Add("TR"), width);
            var height = last.Need(new int[last.Count]);
            for (var c = 0; c < last.Count; c++)
            {
                CellPainter.Paint(new Canvas(canvas, 0, y), last, c, (0, last[c].Lines.Count), (height, last.Offset(c, height)));
            }

            y += height;
        }

        return new Line { Height = y + (last?.BottomBorder ?? 0), Block = canvas.Ops };
    }
}
