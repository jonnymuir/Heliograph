using System.Xml.Linq;
using static Heliograph.Pdf.Reading.WordXml;

namespace Heliograph.Pdf.Checking;

/// <summary>Columns as the converter lays them out: of equal width, with no notes or floating pictures in the letter.</summary>
internal static class ColumnLimits
{
    // Columns of equal width are laid out. Columns of different widths are not yet, a paragraph carrying on into a wider
    // one keeping the narrower one's lines; nor are footnotes, endnotes and floating pictures in a letter set in columns.
    public static IEnumerable<(XElement, string)> Found(XElement root)
    {
        var all = root.Descendants(W + "cols").Where(c => (int?)c.Attribute(W + "num") > 1 || c.Elements(W + "col").Skip(1).Any()).ToList();
        if (all.FirstOrDefault(c => c.Elements(W + "col").Select(col => (string?)col.Attribute(W + "w")).Distinct().Skip(1).Any()
            && (string?)c.Attribute(W + "equalWidth") is "0" or "false") is { } uneven)
        {
            return [(uneven, "columns of different widths")];
        }

        var placed = root.Descendants().Any(e => e.Name == W + "footnoteReference" || e.Name == W + "endnoteReference" || e.Name.LocalName == "anchor");
        return all.Count > 0 && placed ? [(all[0], "footnotes, endnotes or floating pictures")] : [];
    }
}
