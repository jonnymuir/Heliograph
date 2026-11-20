using System.Text.Json;
using Heliograph.Documents;
using Heliograph.Pdf;
using Heliograph.Pdf.LibreOffice;
using Heliograph.Tests.Support;

namespace Heliograph.Tests;

/// <summary>
/// Continuous section breaks in Heliograph's own PDF converter: the next section goes on down the same page with its own
/// margins across it, as Word does it (the way Word's own help gives to change margins part way down a page), and one on a
/// page of another size starts a new page.
/// </summary>
/// <remarks>
/// LibreOffice keeps the margins of the page's first section for the rest of that page, so it is compared down to the
/// break only; Word's own layout of probe-continuous-section is the judge after it.
/// </remarks>
public class PdfContinuousSectionTests
{
    private const string W = "xmlns:w=\"http://schemas.openxmlformats.org/wordprocessingml/2006/main\"";
    private const string Words = "Thank you for your application to the National Juggling Federation, which we received on 3 March and will answer soon.";
    private static readonly FieldDictionary NoFields = new([]);

    [ComparedPdfFact]
    public async Task A_continuous_section_goes_on_down_the_page_with_its_own_margins()
    {
        var docx = Rendered(Letter(narrower: true, otherSize: false));
        var expected = PdfLines.Read(await new LibreOfficePdfConverter().ConvertAsync(docx));
        var actual = PdfLines.Read(await new HeliographPdfConverter().ConvertAsync(docx));

        PdfComparison.FirstDifference(expected, actual, "LibreOffice").Line.Should().BeGreaterThan(4, "the first section's lines are as LibreOffice lays them out");
        actual[4].Top.Should().BeApproximately(expected[4].Top, 1.5, "the next section starts where LibreOffice starts it, down the same page");
        actual.Should().OnlyContain(l => l.Page == 1);
        actual.Where(l => l.Text.StartsWith("Indented", StringComparison.Ordinal)).Should().OnlyContain(l => Math.Abs(l.Left - 144) < 1);
    }

    [PdfLinesFact]
    public async Task A_continuous_section_on_a_page_of_another_size_starts_a_new_page()
    {
        var lines = PdfLines.Read(await new HeliographPdfConverter().ConvertAsync(Rendered(Letter(narrower: false, otherSize: true))));

        lines.First(l => l.Text.StartsWith("Indented", StringComparison.Ordinal)).Page.Should().Be(2);
    }

    // Two paragraphs, a continuous section break, then paragraphs in a section with an inch more margin each side, or on a
    // page of another size.
    internal static MemoryStream Letter(bool narrower, bool otherSize)
    {
        var styles = $"<w:styles {W}><w:docDefaults><w:rPrDefault><w:rPr><w:rFonts w:ascii=\"Arial\" w:hAnsi=\"Arial\"/><w:sz w:val=\"22\"/></w:rPr></w:rPrDefault>"
            + "<w:pPrDefault><w:pPr><w:spacing w:after=\"160\" w:line=\"259\" w:lineRule=\"auto\"/></w:pPr></w:pPrDefault></w:docDefaults></w:styles>";
        var docx = new TestDocx().Part("word/styles.xml", styles, "application/vnd.openxmlformats-officedocument.wordprocessingml.styles+xml")
            .Relationship("http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles", "styles.xml", external: false);
        docx.RawParagraph($"<w:p {W}><w:r><w:t>{Words}</w:t></w:r></w:p>");
        docx.RawParagraph($"<w:p {W}><w:pPr><w:sectPr/></w:pPr><w:r><w:t>{Words}</w:t></w:r></w:p>");
        for (var i = 0; i < 3; i++)
        {
            docx.RawParagraph($"<w:p {W}><w:r><w:t>Indented {Words}</w:t></w:r></w:p>");
        }

        var margins = narrower ? "<w:pgMar w:top=\"1440\" w:right=\"2880\" w:bottom=\"1440\" w:left=\"2880\" w:header=\"720\" w:footer=\"720\" w:gutter=\"0\"/>" : "";
        var size = otherSize ? "<w:pgSz w:w=\"11906\" w:h=\"16838\"/>" : "";
        docx.RawParagraph($"<w:sectPr {W}><w:type w:val=\"continuous\"/>{size}{margins}</w:sectPr>");
        return docx.Build();
    }

    private static byte[] Rendered(MemoryStream template)
    {
        var compilation = DocumentTemplate.Compile(template, NoFields, [new HeliographPdfConverter()]);
        compilation.Diagnostics.Should().BeEmpty();
        return compilation.Document!.Render(JsonDocument.Parse("{}").RootElement, Benchmark.British).Docx!;
    }
}
