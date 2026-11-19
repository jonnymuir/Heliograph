using System.Text.Json;
using Heliograph.Documents;
using Heliograph.Pdf;
using Heliograph.Pdf.LibreOffice;
using Heliograph.Tests.Support;

namespace Heliograph.Tests;

/// <summary>
/// Tables in headers and footers in Heliograph's own PDF converter, which it used to leave out without a word: a letterhead
/// set out as a table, and a footer table with the page number in a cell, laid out as LibreOffice lays them out.
/// </summary>
public class PdfHeaderTableTests
{
    private const string W = "xmlns:w=\"http://schemas.openxmlformats.org/wordprocessingml/2006/main\"";
    private const string R = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
    private static readonly FieldDictionary NoFields = new([]);

    [ComparedPdfFact]
    public async Task Tables_in_the_header_and_footer_are_laid_out_as_LibreOffice_lays_them_out()
    {
        var docx = Rendered(Letter());
        var expected = PdfLines.Read(await new LibreOfficePdfConverter().ConvertAsync(docx));
        var actual = PdfLines.Read(await new HeliographPdfConverter().ConvertAsync(docx));

        PdfComparison.FirstDifference(expected, actual, "LibreOffice").Difference.Should().BeNull();
    }

    [PdfLinesFact]
    public async Task The_header_and_footer_tables_are_drawn_with_the_page_number()
    {
        var lines = PdfLines.Read(await new HeliographPdfConverter().ConvertAsync(Rendered(Letter())));

        lines.Should().Contain(l => l.Text.StartsWith("National Juggling Federation", StringComparison.Ordinal));
        lines.Should().Contain(l => l.Text.Contains("Page 1 of 1", StringComparison.Ordinal));
    }

    private static string Cell(string runs, int width) => $"<w:tc><w:tcPr><w:tcW w:w=\"{width}\" w:type=\"dxa\"/></w:tcPr><w:p>{runs}</w:p></w:tc>";

    private static string Run(string text) => $"<w:r><w:t xml:space=\"preserve\">{text}</w:t></w:r>";

    private static string Table(string row) =>
        $"<w:tbl><w:tblPr><w:tblW w:w=\"9000\" w:type=\"dxa\"/></w:tblPr><w:tblGrid><w:gridCol w:w=\"4500\"/><w:gridCol w:w=\"4500\"/></w:tblGrid><w:tr>{row}</w:tr></w:tbl><w:p/>";

    private static MemoryStream Letter()
    {
        var styles = $"<w:styles {W}><w:docDefaults><w:rPrDefault><w:rPr><w:rFonts w:ascii=\"Arial\" w:hAnsi=\"Arial\"/><w:sz w:val=\"22\"/></w:rPr></w:rPrDefault>"
            + "<w:pPrDefault><w:pPr><w:spacing w:after=\"0\" w:line=\"240\" w:lineRule=\"auto\"/></w:pPr></w:pPrDefault></w:docDefaults></w:styles>";
        var header = $"<w:hdr {W}>{Table(Cell(Run("National Juggling Federation"), 4500) + Cell(Run("12 Sample Street, Clubtown"), 4500))}</w:hdr>";
        var page = $"<w:fldSimple w:instr=\" PAGE \"><w:r><w:t>9</w:t></w:r></w:fldSimple>";
        var pages = $"<w:fldSimple w:instr=\" NUMPAGES \"><w:r><w:t>9</w:t></w:r></w:fldSimple>";
        var footer = $"<w:ftr {W}>{Table(Cell(Run("Membership renewal"), 4500) + Cell(Run("Page ") + page + Run(" of ") + pages, 4500))}</w:ftr>";
        return new TestDocx()
            .Part("word/styles.xml", styles, "application/vnd.openxmlformats-officedocument.wordprocessingml.styles+xml").Relationship($"{R}/styles", "styles.xml", external: false)
            .Part("word/header1.xml", header, "application/vnd.openxmlformats-officedocument.wordprocessingml.header+xml").Relationship($"{R}/header", "header1.xml", external: false)
            .Part("word/footer1.xml", footer, "application/vnd.openxmlformats-officedocument.wordprocessingml.footer+xml").Relationship($"{R}/footer", "footer1.xml", external: false)
            .RawParagraph($"<w:p {W}><w:r><w:t>Dear Ms Example, thank you for your letter.</w:t></w:r></w:p>")
            .RawParagraph($"<w:sectPr {W} xmlns:r=\"{R}\"><w:headerReference w:type=\"default\" r:id=\"rIdX1\"/><w:footerReference w:type=\"default\" r:id=\"rIdX2\"/></w:sectPr>").Build();
    }

    private static byte[] Rendered(MemoryStream template)
    {
        var compilation = DocumentTemplate.Compile(template, NoFields, [new HeliographPdfConverter()]);
        compilation.Diagnostics.Should().BeEmpty();
        return compilation.Document!.Render(JsonDocument.Parse("{}").RootElement, Benchmark.British).Docx!;
    }
}
