using System.Text.Json;
using Heliograph.Documents;
using Heliograph.Pdf;
using Heliograph.Pdf.LibreOffice;
using Heliograph.Tests.Support;

namespace Heliograph.Tests;

/// <summary>
/// Content Word wraps in something else, which Heliograph's own PDF converter used to leave out: paragraphs in custom XML,
/// table rows and cells in content controls (as a repeating section wraps rows); a row that starts further along the
/// grid, whose cells it used to draw from the first column; and what it refuses rather than set out wrongly.
/// </summary>
public class PdfWrappedContentTests
{
    private const string W = "xmlns:w=\"http://schemas.openxmlformats.org/wordprocessingml/2006/main\"";
    private static readonly FieldDictionary NoFields = new([]);

    private static string Cell(string text) => $"<w:tc><w:tcPr><w:tcW w:w=\"3000\" w:type=\"dxa\"/></w:tcPr><w:p><w:r><w:t>{text}</w:t></w:r></w:p></w:tc>";

    private static string Table(string rows) =>
        $"<w:tbl {W}><w:tblPr><w:tblW w:w=\"6000\" w:type=\"dxa\"/></w:tblPr><w:tblGrid><w:gridCol w:w=\"3000\"/><w:gridCol w:w=\"3000\"/></w:tblGrid>{rows}</w:tbl>";

    public static TheoryData<string, string> Wrapped => new()
    {
        { "paragraphs in custom XML", $"<w:customXml {W} w:element=\"notice\"><w:p><w:r><w:t>Your membership renews in April.</w:t></w:r></w:p><w:p><w:r><w:t>The fee is unchanged.</w:t></w:r></w:p></w:customXml>" },
        { "table rows in a content control", Table($"<w:tr>{Cell("Membership")}{Cell("£45.00")}</w:tr><w:sdt><w:sdtPr><w:id w:val=\"1\"/></w:sdtPr><w:sdtContent><w:tr>{Cell("Newsletter")}{Cell("£5.00")}</w:tr><w:tr>{Cell("Insurance")}{Cell("£12.50")}</w:tr></w:sdtContent></w:sdt>") },
        { "a cell in a content control", Table($"<w:tr>{Cell("Membership")}<w:sdt><w:sdtPr><w:id w:val=\"2\"/></w:sdtPr><w:sdtContent>{Cell("£45.00")}</w:sdtContent></w:sdt></w:tr>") },
    };

    [ComparedPdfTheory]
    [MemberData(nameof(Wrapped))]
    public async Task Wrapped_content_is_laid_out_as_LibreOffice_lays_it_out(string kind, string block)
    {
        var docx = Rendered(Letter(block));
        var expected = PdfLines.Read(await new LibreOfficePdfConverter().ConvertAsync(docx));
        var actual = PdfLines.Read(await new HeliographPdfConverter().ConvertAsync(docx));

        PdfComparison.FirstDifference(expected, actual, "LibreOffice").Difference.Should().BeNull(kind);
    }

    // LibreOffice sets a whole table across the page as it does not in Word (PdfNestedTableTests), so it is the row's place
    // against the row above that is compared.
    [ComparedPdfFact]
    public async Task A_row_that_starts_further_along_the_grid_is_laid_out_as_LibreOffice_lays_it_out()
    {
        var table = Table($"<w:tr>{Cell("Membership")}{Cell("£45.00")}</w:tr><w:tr><w:trPr><w:gridBefore w:val=\"1\"/><w:wBefore w:w=\"3000\" w:type=\"dxa\"/></w:trPr>{Cell("Total £45.00")}</w:tr>");
        var docx = Rendered(Letter(table));
        double Indent(List<PdfLine> lines) => lines.Single(l => l.Text == "Total £45.00").Left - lines.Single(l => l.Text.StartsWith("Membership", StringComparison.Ordinal)).Left;

        var (theirs, ours) = (PdfLines.Read(await new LibreOfficePdfConverter().ConvertAsync(docx)), PdfLines.Read(await new HeliographPdfConverter().ConvertAsync(docx)));

        PdfComparison.FirstDifference(theirs, ours, "LibreOffice").Difference.Should().BeNull();
        Indent(ours).Should().BeApproximately(Indent(theirs), 1.5).And.BeApproximately(150, 1.5);
    }

    [Theory]
    [InlineData("<w:p {W}><w:pPr><w:framePr w:dropCap=\"drop\" w:lines=\"3\" w:wrap=\"around\" w:vAnchor=\"text\" w:hAnchor=\"text\"/></w:pPr><w:r><w:t>D</w:t></w:r></w:p>", "drop capital")]
    [InlineData("<w:p {W}><w:pPr><w:framePr w:w=\"2000\" w:hSpace=\"180\" w:wrap=\"around\" w:vAnchor=\"text\" w:hAnchor=\"page\" w:x=\"1000\" w:y=\"1\"/></w:pPr><w:r><w:t>Framed</w:t></w:r></w:p>", "in a frame")]
    [InlineData("<w:p {W}><w:pPr><w:jc w:val=\"distribute\"/></w:pPr><w:r><w:t>Spread</w:t></w:r></w:p>", "whole line")]
    [InlineData("<w:sdt {W}><w:sdtContent><w:p><w:pPr><w:sectPr/></w:pPr><w:r><w:t>Before the break</w:t></w:r></w:p></w:sdtContent></w:sdt>", "content control")]
    public void Content_it_would_set_out_wrongly_is_refused_at_compile(string xml, string named)
    {
        var docx = new TestDocx().RawParagraph(xml.Replace("{W}", W, StringComparison.Ordinal)).Paragraph("After").Build();

        DocumentTemplate.Compile(docx, NoFields, [new HeliographPdfConverter()]).Diagnostics
            .Should().Contain(d => d.Code == DiagnosticCodes.PdfCannotDrawShape && d.Message.Contains(named, StringComparison.Ordinal));
    }

    private static MemoryStream Letter(string block)
    {
        var styles = $"<w:styles {W}><w:docDefaults><w:rPrDefault><w:rPr><w:rFonts w:ascii=\"Arial\" w:hAnsi=\"Arial\"/><w:sz w:val=\"22\"/></w:rPr></w:rPrDefault>"
            + "<w:pPrDefault><w:pPr><w:spacing w:after=\"160\" w:line=\"259\" w:lineRule=\"auto\"/></w:pPr></w:pPrDefault></w:docDefaults></w:styles>";
        return new TestDocx().Part("word/styles.xml", styles, "application/vnd.openxmlformats-officedocument.wordprocessingml.styles+xml")
            .Relationship("http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles", "styles.xml", external: false)
            .RawParagraph($"<w:p {W}><w:r><w:t>Dear Ms Example,</w:t></w:r></w:p>").RawParagraph(block)
            .RawParagraph($"<w:p {W}><w:r><w:t>Yours sincerely,</w:t></w:r></w:p>").Build();
    }

    private static byte[] Rendered(MemoryStream template)
    {
        var compilation = DocumentTemplate.Compile(template, NoFields, [new HeliographPdfConverter()]);
        compilation.Diagnostics.Should().BeEmpty();
        return compilation.Document!.Render(JsonDocument.Parse("{}").RootElement, Benchmark.British).Docx!;
    }
}
