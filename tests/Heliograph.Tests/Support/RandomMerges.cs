namespace Heliograph.Tests.Support;

/// <summary>The merges RandomTables makes: a table's first column merged down a few rows at a time.</summary>
internal static class RandomMerges
{
    // A third of tables merge their first column down a few rows at a time, where that column's cells span no further.
    public static string Into(string table, Random random)
    {
        if (random.Next(3) != 0)
        {
            return table;
        }

        var rows = System.Text.RegularExpressions.Regex.Split(table, "(?=<w:tr>)");
        var run = 0;
        for (var r = 1; r < rows.Length; r++)
        {
            run = Next(run, Plain(rows[r]), random);
            rows[r] = run switch
            {
                1 => Mark(rows[r], "<w:vMerge w:val=\"restart\"/>", empty: false),
                > 1 => Mark(rows[r], "<w:vMerge/>", empty: true),
                _ => rows[r],
            };
        }

        return string.Concat(rows);
    }

    // A row whose first cell spans one column only.
    private static bool Plain(string row) => row.StartsWith("<w:tr>", StringComparison.Ordinal) && !First(row).Contains("gridSpan", StringComparison.Ordinal);

    // How far into a merge a row is: 0 for none, 1 where one starts, more as it carries on.
    private static int Next(int run, bool plain, Random random)
    {
        if (!plain)
        {
            return 0;
        }

        return run > 0 && random.Next(3) != 0 ? run + 1 : random.Next(4) == 0 ? 1 : 0;
    }

    private static string First(string row) => row[..(row.IndexOf("</w:tc>", StringComparison.Ordinal) + 7)];

    // The row's first cell marked as merged; a cell merged into the one above keeps an empty paragraph, as Word saves it.
    private static string Mark(string row, string merge, bool empty)
    {
        var first = First(row);
        var marked = first.Replace("</w:tcPr>", merge + "</w:tcPr>", StringComparison.Ordinal);
        if (empty)
        {
            var start = marked.IndexOf("<w:p>", StringComparison.Ordinal);
            marked = marked[..start] + "<w:p/></w:tc>";
        }

        return marked + row[first.Length..];
    }
}
