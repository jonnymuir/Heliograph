using System.Text.Json;
using Heliograph.Documents;
using Heliograph.Pdf;
using Heliograph.Pdf.LibreOffice;
using Heliograph.Tests.Support;

namespace Heliograph.Tests;

/// <summary>
/// A table inside a table's cell in Heliograph's own PDF converter: laid out across the cell's text, with its borders and
/// padding, the outer row as tall as it needs, laid out as LibreOffice lays it out down the page; and tagged as a table in
/// the cell.
/// </summary>
/// <remarks>
/// Across the page LibreOffice puts a table's edge on the margin in a document with no compatibility settings, where Word,
/// and the converter, put its text there (Word's own PDF of probe-tables shows it); Word's layout of probe-nested-table is
/// the judge across.
/// </remarks>
public class PdfNestedTableTests
{
    private const string W = "xmlns:w=\"http://schemas.openxmlformats.org/wordprocessingml/2006/main\"";
    private static readonly FieldDictionary NoFields = new([]);
    private const string Borders = "<w:tblBorders><w:top w:val=\"single\" w:sz=\"4\" w:color=\"auto\"/><w:left w:val=\"single\" w:sz=\"4\" w:color=\"auto\"/><w:bottom w:val=\"single\" w:sz=\"4\" w:color=\"auto\"/>"
        + "<w:right w:val=\"single\" w:sz=\"4\" w:color=\"auto\"/><w:insideH w:val=\"single\" w:sz=\"4\" w:color=\"auto\"/><w:insideV w:val=\"single\" w:sz=\"4\" w:color=\"auto\"/></w:tblBorders>";

    // With nothing after the inner table, its cell still ends with the empty paragraph Word requires there, which Word shows
    // as a line and LibreOffice does not; Word's layout of probe-nested-table-alone is the judge of that one.
    public static TheoryData<string, bool> Letters => new() { { "text before and after the inner table", true } };

    [ComparedPdfTheory]
    [MemberData(nameof(Letters))]
    public async Task A_table_inside_a_table_is_laid_out_as_LibreOffice_lays_it_out(string kind, bool around)
    {
        var docx = Rendered(Letter(around));
        var expected = PdfLines.Read(await new LibreOfficePdfConverter().ConvertAsync(docx));
        var actual = PdfLines.Read(await new HeliographPdfConverter().ConvertAsync(docx));

        PdfComparison.FirstDifference(expected, actual, "LibreOffice").Difference.Should().BeNull(kind);
    }

    [VeraPdfTheory]
    [InlineData(true)]
    public async Task A_table_inside_a_table_is_still_an_accessible_PDF(bool around) =>
        PdfValidator.FailuresAsPdfUa(await new HeliographPdfConverter().ConvertAsync(Rendered(Letter(around)))).Should().BeEmpty();

    // A two-column table whose second cell holds a table of fees, two columns by three rows.
    internal static MemoryStream Letter(bool around)
    {
        static string Cell(string text, int width) => $"<w:tc><w:tcPr><w:tcW w:w=\"{width}\" w:type=\"dxa\"/></w:tcPr><w:p><w:r><w:t>{text}</w:t></w:r></w:p></w:tc>";
        var inner = $"<w:tbl><w:tblPr><w:tblW w:w=\"4000\" w:type=\"dxa\"/>{Borders}</w:tblPr><w:tblGrid><w:gridCol w:w=\"2500\"/><w:gridCol w:w=\"1500\"/></w:tblGrid>"
            + $"<w:tr>{Cell("Membership", 2500)}{Cell("£45.00", 1500)}</w:tr><w:tr>{Cell("Newsletter", 2500)}{Cell("£5.00", 1500)}</w:tr><w:tr>{Cell("Total", 2500)}{Cell("£50.00", 1500)}</w:tr></w:tbl>";
        var holder = $"<w:tc><w:tcPr><w:tcW w:w=\"5000\" w:type=\"dxa\"/></w:tcPr>{(around ? "<w:p><w:r><w:t>Your fees for the year:</w:t></w:r></w:p>" : "")}{inner}<w:p><w:r><w:t>{(around ? "Please pay by March." : "")}</w:t></w:r></w:p></w:tc>";
        var outer = $"<w:tbl {W}><w:tblPr><w:tblW w:w=\"9000\" w:type=\"dxa\"/>{Borders}</w:tblPr><w:tblGrid><w:gridCol w:w=\"4000\"/><w:gridCol w:w=\"5000\"/></w:tblGrid>"
            + $"<w:tr>{Cell("Ms Example", 4000)}{holder}</w:tr><w:tr>{Cell("Mr Sample", 4000)}{Cell("No fees due.", 5000)}</w:tr></w:tbl>";
        var styles = $"<w:styles {W}><w:docDefaults><w:rPrDefault><w:rPr><w:rFonts w:ascii=\"Arial\" w:hAnsi=\"Arial\"/><w:sz w:val=\"22\"/></w:rPr></w:rPrDefault>"
            + "<w:pPrDefault><w:pPr><w:spacing w:after=\"0\" w:line=\"240\" w:lineRule=\"auto\"/></w:pPr></w:pPrDefault></w:docDefaults>"
            + "<w:style w:type=\"table\" w:default=\"1\" w:styleId=\"TableNormal\"><w:name w:val=\"Normal Table\"/><w:tblPr><w:tblInd w:w=\"0\" w:type=\"dxa\"/>"
            + "<w:tblCellMar><w:top w:w=\"0\" w:type=\"dxa\"/><w:left w:w=\"108\" w:type=\"dxa\"/><w:bottom w:w=\"0\" w:type=\"dxa\"/><w:right w:w=\"108\" w:type=\"dxa\"/></w:tblCellMar></w:tblPr></w:style></w:styles>";
        return new TestDocx().Part("word/styles.xml", styles, "application/vnd.openxmlformats-officedocument.wordprocessingml.styles+xml")
            .Relationship("http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles", "styles.xml", external: false)
            .RawParagraph($"<w:p {W}><w:r><w:t>Dear members,</w:t></w:r></w:p>").RawParagraph(outer).RawParagraph($"<w:p {W}><w:r><w:t>Thank you.</w:t></w:r></w:p>").Build();
    }

    private static byte[] Rendered(MemoryStream template)
    {
        var compilation = DocumentTemplate.Compile(template, NoFields, [new HeliographPdfConverter()]);
        compilation.Diagnostics.Should().BeEmpty();
        return compilation.Document!.Render(JsonDocument.Parse("{}").RootElement, Benchmark.British).Docx!;
    }
}
