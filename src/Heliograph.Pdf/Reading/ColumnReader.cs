using System.Xml.Linq;
using static Heliograph.Pdf.Reading.WordXml;

namespace Heliograph.Pdf.Reading;

/// <summary>
/// A section's columns, as Word sets them: so many of equal width with the same space between, half an inch unless it
/// says, or each with its own width and space; and a line between them when it asks. Null for a single column.
/// </summary>
internal static class ColumnReader
{
    public static ColumnSet? Read(XElement sect, PageGeometry page)
    {
        var cols = sect.Element(W + "cols");
        var own = cols?.Elements(W + "col").ToList() ?? [];
        var equal = (string?)cols?.Attribute(W + "equalWidth") is not ("0" or "false") || own.Count == 0;
        var count = equal ? (int?)cols?.Attribute(W + "num") ?? 1 : own.Count;
        if (cols is null || count < 2)
        {
            return null;
        }

        var columns = equal ? Equal(count, Twips(cols, "space") ?? 36, page) : Own(own, page.ContentWidth / count);
        return new ColumnSet(columns, (string?)cols.Attribute(W + "sep") is "1" or "true" or "on");
    }

    // So many columns of equal width, the same space after each but the last.
    private static List<(double Width, double Space)> Equal(int count, double space, PageGeometry page)
    {
        var width = (page.ContentWidth - (space * (count - 1))) / count;
        return [.. Enumerable.Range(0, count).Select(i => (width, i < count - 1 ? space : 0))];
    }

    // Columns each with the width and space after it that it sets.
    private static List<(double Width, double Space)> Own(List<XElement> columns, double otherwise) =>
        [.. columns.Select((c, i) => (Twips(c, "w") ?? otherwise, i < columns.Count - 1 ? Twips(c, "space") ?? 0 : 0))];
}
