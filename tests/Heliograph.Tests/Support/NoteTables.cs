namespace Heliograph.Tests.Support;

/// <summary>A table for the note letters, with a footnote referred to from one of its cells.</summary>
internal static class NoteTables
{
    private const string W = "xmlns:w=\"http://schemas.openxmlformats.org/wordprocessingml/2006/main\"";

    /// <summary>
    /// A table of fees, the second row's first cell referring to a footnote. Word writes notes in the order the letter refers
    /// to them, and LibreOffice matches each to its reference by that order, so the note's number should be its place.
    /// </summary>
    public static string FeesTable(int note) =>
        $"<w:tbl {W}><w:tblPr><w:tblW w:w=\"9000\" w:type=\"dxa\"/></w:tblPr><w:tblGrid><w:gridCol w:w=\"4500\"/><w:gridCol w:w=\"4500\"/></w:tblGrid>"
        + $"<w:tr>{FeeCell}Membership{CellEnd}{FeeCell}£45.00{CellEnd}</w:tr>"
        + $"<w:tr>{FeeCell}Late charge</w:t></w:r><w:r><w:rPr><w:rStyle w:val=\"FootnoteReference\"/></w:rPr><w:footnoteReference w:id=\"{note}\"/></w:r></w:p></w:tc>{FeeCell}£10.00{CellEnd}</w:tr>"
        + $"<w:tr>{FeeCell}Newsletter{CellEnd}{FeeCell}£5.00{CellEnd}</w:tr></w:tbl>";

    private const string FeeCell = "<w:tc><w:tcPr><w:tcW w:w=\"4500\" w:type=\"dxa\"/></w:tcPr><w:p><w:r><w:t>";
    private const string CellEnd = "</w:t></w:r></w:p></w:tc>";
}
