using System.Text.Json;
using Heliograph.Documents;
using Heliograph.Pdf;
using Heliograph.Pdf.LibreOffice;
using Heliograph.Tests.Support;

namespace Heliograph.Tests;

/// <summary>
/// Tables in Heliograph's own PDF converter: their style's borders, padding and paragraph settings (Word's Table Grid),
/// row heights, tables centred or to the right, and text at the middle or foot of a cell, laid out as LibreOffice lays
/// them out; and what it cannot lay out yet refused at compile.
/// </summary>
public class PdfTableTests
{
    private const string W = "xmlns:w=\"http://schemas.openxmlformats.org/wordprocessingml/2006/main\"";
    private static readonly FieldDictionary NoFields = new([]);

    // Word's Table Grid: single borders all round and between, and no space after paragraphs in its cells.
    private const string TableGrid = "<w:style w:type=\"table\" w:styleId=\"TableGrid\"><w:name w:val=\"Table Grid\"/><w:basedOn w:val=\"TableNormal\"/>"
        + "<w:pPr><w:spacing w:after=\"0\" w:line=\"240\" w:lineRule=\"auto\"/></w:pPr><w:tblPr><w:tblBorders>"
        + "<w:top w:val=\"single\" w:sz=\"4\" w:space=\"0\" w:color=\"auto\"/><w:left w:val=\"single\" w:sz=\"4\" w:space=\"0\" w:color=\"auto\"/>"
        + "<w:bottom w:val=\"single\" w:sz=\"4\" w:space=\"0\" w:color=\"auto\"/><w:right w:val=\"single\" w:sz=\"4\" w:space=\"0\" w:color=\"auto\"/>"
        + "<w:insideH w:val=\"single\" w:sz=\"4\" w:space=\"0\" w:color=\"auto\"/><w:insideV w:val=\"single\" w:sz=\"4\" w:space=\"0\" w:color=\"auto\"/>"
        + "</w:tblBorders></w:tblPr></w:style>";

    private const string TableNormal = "<w:style w:type=\"table\" w:default=\"1\" w:styleId=\"TableNormal\"><w:name w:val=\"Normal Table\"/>"
        + "<w:tblPr><w:tblInd w:w=\"0\" w:type=\"dxa\"/><w:tblCellMar><w:top w:w=\"0\" w:type=\"dxa\"/><w:left w:w=\"108\" w:type=\"dxa\"/>"
        + "<w:bottom w:w=\"0\" w:type=\"dxa\"/><w:right w:w=\"108\" w:type=\"dxa\"/></w:tblCellMar></w:tblPr></w:style>";

    public static TheoryData<string, string, string, string> Tables => new()
    {
        { "Table Grid", "<w:tblStyle w:val=\"TableGrid\"/>", "", "" },
        { "centred", "<w:tblStyle w:val=\"TableGrid\"/><w:jc w:val=\"center\"/>", "", "" },
        { "to the right", "<w:tblStyle w:val=\"TableGrid\"/><w:jc w:val=\"right\"/>", "", "" },
        { "rows at least 30pt", "<w:tblStyle w:val=\"TableGrid\"/>", "<w:trHeight w:val=\"600\"/>", "" },
        { "rows exactly 24pt", "<w:tblStyle w:val=\"TableGrid\"/>", "<w:trHeight w:val=\"480\" w:hRule=\"exact\"/>", "" },
        { "text in the middle", "<w:tblStyle w:val=\"TableGrid\"/>", "<w:trHeight w:val=\"900\"/>", "<w:vAlign w:val=\"center\"/>" },
        { "text at the foot", "<w:tblStyle w:val=\"TableGrid\"/>", "<w:trHeight w:val=\"900\"/>", "<w:vAlign w:val=\"bottom\"/>" },
    };

    [ComparedPdfTheory]
    [MemberData(nameof(Tables))]
    public async Task A_table_is_laid_out_as_LibreOffice_lays_it_out(string kind, string tblPr, string trPr, string tcPr)
    {
        var docx = Rendered(Letter(tblPr, trPr, tcPr));
        var expected = PdfLines.Read(await new LibreOfficePdfConverter().ConvertAsync(docx));
        var actual = PdfLines.Read(await new HeliographPdfConverter().ConvertAsync(docx));

        actual.Select(l => l.Text).Should().Equal(expected.Select(l => l.Text), kind);
        actual.Zip(expected).Should().OnlyContain(pair => Math.Abs(pair.First.Top - pair.Second.Top) <= 1.5, kind);
        actual.Zip(expected).Should().OnlyContain(pair => Math.Abs(pair.First.Left - pair.Second.Left) <= 1.5, kind);
    }

    [PdfLinesFact]
    public async Task A_table_styles_inside_borders_are_drawn_between_its_cells()
    {
        var page = PdfRender.FirstPage(await new HeliographPdfConverter().ConvertAsync(Rendered(Letter("<w:tblStyle w:val=\"TableGrid\"/>", "", ""))));
        var line = PdfLines.Read(await new HeliographPdfConverter().ConvertAsync(Rendered(Letter("<w:tblStyle w:val=\"TableGrid\"/>", "", ""))))
            .First(l => l.Text.StartsWith("Membership", StringComparison.Ordinal));

        // The border between the two columns, 180pt from the table's left edge (a cell's padding left of the text).
        Enumerable.Range(0, 4).Select(dx => page.At(line.Left - 5.4 + 180 - 1 + dx, line.Top + 4)).Should().Contain(c => c.R < 128);
    }

    [Theory]
    [InlineData("<w:tblStyle w:val=\"Banded\"/><w:tblLook w:val=\"04A0\" w:firstRow=\"1\" w:lastRow=\"0\" w:firstColumn=\"1\" w:lastColumn=\"0\" w:noHBand=\"0\" w:noVBand=\"1\"/>", "rows or columns")]
    [InlineData("<w:tblStyle w:val=\"TableGrid\"/><w:tblpPr w:leftFromText=\"180\" w:rightFromText=\"180\" w:vertAnchor=\"text\" w:tblpY=\"1\"/>", "floats")]
    public void A_table_it_cannot_lay_out_is_refused_at_compile(string tblPr, string named)
    {
        var compilation = DocumentTemplate.Compile(Letter(tblPr, "", ""), NoFields, [new HeliographPdfConverter()]);

        compilation.Diagnostics.Should().Contain(d => d.Code == DiagnosticCodes.PdfCannotLayOutTable && d.Message.Contains(named, StringComparison.Ordinal));
    }

    [Fact]
    public void A_table_whose_style_formats_rows_the_table_turns_off_is_laid_out()
    {
        var off = "<w:tblStyle w:val=\"Banded\"/><w:tblLook w:firstRow=\"0\" w:lastRow=\"0\" w:firstColumn=\"0\" w:lastColumn=\"0\" w:noHBand=\"1\" w:noVBand=\"1\"/>";

        DocumentTemplate.Compile(Letter(off, "", ""), NoFields, [new HeliographPdfConverter()]).Diagnostics.Should().BeEmpty();
    }

    // A two-column table of membership fees between two paragraphs.
    private static MemoryStream Letter(string tblPr, string trPr, string tcPr)
    {
        var banded = "<w:style w:type=\"table\" w:styleId=\"Banded\"><w:name w:val=\"Banded\"/><w:basedOn w:val=\"TableGrid\"/>"
            + "<w:tblStylePr w:type=\"firstRow\"><w:rPr><w:b/></w:rPr></w:tblStylePr><w:tblStylePr w:type=\"band1Horz\"><w:tcPr><w:shd w:val=\"clear\" w:fill=\"D9E2F3\"/></w:tcPr></w:tblStylePr></w:style>";
        var styles = $"<w:styles {W}><w:docDefaults><w:rPrDefault><w:rPr><w:rFonts w:ascii=\"Arial\" w:hAnsi=\"Arial\"/><w:sz w:val=\"22\"/></w:rPr></w:rPrDefault>"
            + "<w:pPrDefault><w:pPr><w:spacing w:after=\"160\" w:line=\"259\" w:lineRule=\"auto\"/></w:pPr></w:pPrDefault></w:docDefaults>"
            + $"<w:style w:type=\"paragraph\" w:default=\"1\" w:styleId=\"Normal\"><w:name w:val=\"Normal\"/></w:style>{TableNormal}{TableGrid}{banded}</w:styles>";
        var docx = new TestDocx().Part("word/styles.xml", styles, "application/vnd.openxmlformats-officedocument.wordprocessingml.styles+xml")
            .Relationship("http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles", "styles.xml", external: false);
        docx.RawParagraph($"<w:p {W}><w:r><w:t>Your fees for the year are below.</w:t></w:r></w:p>");
        var rows = new[] { ("Membership", "£45.00"), ("Insurance at events", "£12.50"), ("Newsletter by post", "£3.50") }
            .Select(r => $"<w:tr><w:trPr>{trPr}</w:trPr><w:tc><w:tcPr><w:tcW w:w=\"3600\" w:type=\"dxa\"/>{tcPr}</w:tcPr><w:p><w:r><w:t>{r.Item1}</w:t></w:r></w:p></w:tc>"
                + $"<w:tc><w:tcPr><w:tcW w:w=\"1800\" w:type=\"dxa\"/>{tcPr}</w:tcPr><w:p><w:r><w:t>{r.Item2}</w:t></w:r></w:p></w:tc></w:tr>");
        docx.RawParagraph($"<w:tbl {W}><w:tblPr>{tblPr}<w:tblW w:w=\"5400\" w:type=\"dxa\"/></w:tblPr><w:tblGrid><w:gridCol w:w=\"3600\"/><w:gridCol w:w=\"1800\"/></w:tblGrid>{string.Concat(rows)}</w:tbl>");
        docx.RawParagraph($"<w:p {W}><w:r><w:t>Please pay by the end of March.</w:t></w:r></w:p>");
        return docx.Build();
    }

    private static byte[] Rendered(MemoryStream template)
    {
        var compilation = DocumentTemplate.Compile(template, NoFields, [new HeliographPdfConverter()]);
        compilation.Diagnostics.Should().BeEmpty();
        return compilation.Document!.Render(JsonDocument.Parse("{}").RootElement, Benchmark.British).Docx!;
    }
}
