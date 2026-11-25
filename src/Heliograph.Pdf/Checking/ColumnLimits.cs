using System.Xml.Linq;
using static Heliograph.Pdf.Reading.WordXml;

namespace Heliograph.Pdf.Checking;

/// <summary>Columns as the converter lays them out: of equal width or each of its own, with no notes or floating pictures in the letter.</summary>
internal static class ColumnLimits
{
    // Columns are laid out; footnotes, endnotes and floating pictures in a letter set in columns are not yet.
    public static IEnumerable<(XElement, string)> Found(XElement root)
    {
        var all = root.Descendants(W + "cols").Where(c => (int?)c.Attribute(W + "num") > 1 || c.Elements(W + "col").Skip(1).Any()).ToList();
        var placed = root.Descendants().Any(e => e.Name == W + "footnoteReference" || e.Name == W + "endnoteReference" || e.Name.LocalName == "anchor");
        return all.Count > 0 && placed ? [(all[0], "footnotes, endnotes or floating pictures")] : [];
    }
}
