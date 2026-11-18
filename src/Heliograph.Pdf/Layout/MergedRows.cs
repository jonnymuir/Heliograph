using Heliograph.Pdf.Reading;

namespace Heliograph.Pdf.Layout;

/// <summary>
/// Rows joined by cells merged down a column: where such a run of rows ends, how tall each row is (its own cells' height,
/// the last row of a merged cell growing to fit that cell's text), and how far down a merged cell reaches.
/// </summary>
internal static class MergedRows
{
    /// <summary>The last row joined to this one by a merged cell; this row itself when none starts here.</summary>
    public static int End(IReadOnlyList<Row> rows, int r)
    {
        if (!rows[r].Cells.Any(c => c.VMerge == "restart"))
        {
            return r;
        }

        var end = r;
        while (end + 1 < rows.Count && rows[end + 1].Cells.Any(c => c.VMerge == "continue"))
        {
            end++;
        }

        return end;
    }

    /// <summary>Each row's height: what its own cells need, the last row a merged cell reaches growing to fit its text.</summary>
    public static double[] Heights(List<RowLayout> group)
    {
        var heights = group.Select(row => row.OwnNeed()).ToArray();
        for (var k = 0; k < group.Count; k++)
        {
            for (var c = 0; c < group[k].Count; c++)
            {
                if (group[k].Source[c].VMerge == "restart")
                {
                    var last = Last(group, k, group[k].Columns[c]);
                    var sum = heights[k..(last + 1)].Sum();
                    heights[last] += Math.Max(0, group[k].Natural(c) - sum);
                }
            }
        }

        return heights;
    }

    /// <summary>How tall a merged cell is: the rows it reaches down, added up.</summary>
    public static double Down(List<RowLayout> group, double[] heights, int k, int c) => heights[k..(Last(group, k, group[k].Columns[c]) + 1)].Sum();

    // Whether a row's cell in this grid column is merged into the one above.
    private static bool Continues(RowLayout row, int column) =>
        row.Source.Where((cell, c) => row.Columns[c] == column).Any(cell => cell.VMerge == "continue");

    // The last row below this one whose cell in the same grid column continues the merge.
    private static int Last(List<RowLayout> group, int k, int column)
    {
        var last = k;
        while (last + 1 < group.Count && Continues(group[last + 1], column))
        {
            last++;
        }

        return last;
    }
}
