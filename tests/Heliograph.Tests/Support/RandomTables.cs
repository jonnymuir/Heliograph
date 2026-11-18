using System.Text;

namespace Heliograph.Tests.Support;

/// <summary>
/// What RandomLayout adds to its tables beyond borders, padding, spans and heading rows: Word's Table Grid style, a table
/// centred or to the right, row heights at least or exactly so, and text at the middle or foot of a cell.
/// </summary>
internal static class RandomTables
{
    /// <summary>The styles a table may name: Word's Table Normal, which every table takes unless it names another, and Table Grid.</summary>
    public const string Styles =
        "<w:style w:type=\"table\" w:default=\"1\" w:styleId=\"TableNormal\"><w:name w:val=\"Normal Table\"/><w:tblPr><w:tblInd w:w=\"0\" w:type=\"dxa\"/>"
        + "<w:tblCellMar><w:top w:w=\"0\" w:type=\"dxa\"/><w:left w:w=\"108\" w:type=\"dxa\"/><w:bottom w:w=\"0\" w:type=\"dxa\"/><w:right w:w=\"108\" w:type=\"dxa\"/></w:tblCellMar></w:tblPr></w:style>"
        + "<w:style w:type=\"table\" w:styleId=\"TableGrid\"><w:name w:val=\"Table Grid\"/><w:basedOn w:val=\"TableNormal\"/>"
        + "<w:pPr><w:spacing w:after=\"0\" w:line=\"240\" w:lineRule=\"auto\"/></w:pPr><w:tblPr><w:tblBorders>"
        + "<w:top w:val=\"single\" w:sz=\"4\" w:space=\"0\" w:color=\"auto\"/><w:left w:val=\"single\" w:sz=\"4\" w:space=\"0\" w:color=\"auto\"/>"
        + "<w:bottom w:val=\"single\" w:sz=\"4\" w:space=\"0\" w:color=\"auto\"/><w:right w:val=\"single\" w:sz=\"4\" w:space=\"0\" w:color=\"auto\"/>"
        + "<w:insideH w:val=\"single\" w:sz=\"4\" w:space=\"0\" w:color=\"auto\"/><w:insideV w:val=\"single\" w:sz=\"4\" w:space=\"0\" w:color=\"auto\"/>"
        + "</w:tblBorders></w:tblPr></w:style>";

    public static string Into(string table, Random random)
    {
        var style = random.Next(2) == 0 ? "<w:tblStyle w:val=\"TableGrid\"/>" : "";
        var align = random.Next(4) switch { 0 => "<w:jc w:val=\"center\"/>", 1 => "<w:jc w:val=\"right\"/>", _ => "" };
        table = table.Replace("<w:tblPr>", $"<w:tblPr>{style}", StringComparison.Ordinal).Replace("</w:tblPr>", $"{align}</w:tblPr>", StringComparison.Ordinal);
        return Merges(Cells(Rows(table, random), random), random);
    }

    // A third of tables merge their first column down a few rows at a time, where that column's cells span no further.
    private static string Merges(string table, Random random)
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

    // A fifth of rows set a height: at least, or exactly, 18 to 40 points.
    private static string Rows(string table, Random random) => Each(table, "<w:tr><w:tc>", () => random.Next(5) != 0 ? "<w:tr><w:tc>"
        : $"<w:tr><w:trPr><w:trHeight w:val=\"{random.Next(18, 40) * 20}\"{(random.Next(3) == 0 ? " w:hRule=\"exact\"" : "")}/></w:trPr><w:tc>");

    // A quarter of cells set their text at the middle or the foot, and a sixth are shaded.
    private static string Cells(string table, Random random) => Each(table, "</w:tcPr>", () =>
        (random.Next(6) == 0 ? "<w:shd w:val=\"clear\" w:color=\"auto\" w:fill=\"FFF2CC\"/>" : "")
        + (random.Next(4) != 0 ? "</w:tcPr>" : $"<w:vAlign w:val=\"{(random.Next(2) == 0 ? "center" : "bottom")}\"/></w:tcPr>"));

    private static string Each(string text, string marker, Func<string> replacement)
    {
        var parts = text.Split(marker);
        var result = new StringBuilder(parts[0]);
        foreach (var part in parts.Skip(1))
        {
            result.Append(replacement()).Append(part);
        }

        return result.ToString();
    }
}
