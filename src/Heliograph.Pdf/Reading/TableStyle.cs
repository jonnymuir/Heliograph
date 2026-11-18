using System.Xml.Linq;
using static Heliograph.Pdf.Reading.WordXml;

namespace Heliograph.Pdf.Reading;

/// <summary>
/// A table's properties through its style (Table Grid, say, or Word's Table Normal when it names none), everything that
/// style is based on, and what the table sets itself, the last that sets each one winning. A table style also carries
/// paragraph and run properties for the text in its cells.
/// </summary>
internal sealed class TableStyle
{
    private readonly List<XElement> _tables;

    public TableStyle(XElement tbl, StyleSheet styles)
    {
        var tblPr = tbl.Element(W + "tblPr");
        var chain = styles.Chain(Val(tblPr?.Element(W + "tblStyle")) ?? styles.DefaultTable ?? "");
        _tables = [.. chain.Select(s => s.Element(W + "tblPr")).Append(tblPr).OfType<XElement>()];
        Paragraph = chain.Select(s => s.Element(W + "pPr")).OfType<XElement>().ToList();
        Run = chain.Select(s => s.Element(W + "rPr")).OfType<XElement>().ToList();
    }

    /// <summary>The paragraph properties the style gives text in its cells, the most basic first.</summary>
    public IReadOnlyList<XElement> Paragraph { get; }

    /// <summary>The run properties the style gives text in its cells, the most basic first.</summary>
    public IReadOnlyList<XElement> Run { get; }

    /// <summary>A property of the table, such as jc or tblInd, from whichever layer last sets it.</summary>
    public XElement? Property(string name) => _tables.Select(t => t.Element(W + name)).LastOrDefault(e => e is not null);

    /// <summary>One of a group's children, such as tblBorders' top or tblCellMar's left, from whichever layer last sets it.</summary>
    public XElement? Property(string group, string name) => _tables.Select(t => t.Element(W + group)?.Element(W + name)).LastOrDefault(e => e is not null);
}
