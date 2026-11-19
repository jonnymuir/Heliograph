using System.Text.Json;
using Heliograph.Documents;
using Heliograph.Pdf;
using Heliograph.Pdf.LibreOffice;
using Heliograph.Tests.Support;

namespace Heliograph.Tests;

/// <summary>
/// Page breaks typed in the text (Ctrl+Enter in Word) in Heliograph's own PDF converter: the rest of the paragraph goes on
/// at the top of the next page, a paragraph that holds only a break leaving its empty line there, as LibreOffice lays it
/// out; a column break in a single column does the same. Before, a page break was read as a line break.
/// </summary>
public class PdfPageBreakTests
{
    private const string W = "xmlns:w=\"http://schemas.openxmlformats.org/wordprocessingml/2006/main\"";
    private static readonly FieldDictionary NoFields = new([]);

    public static TheoryData<string, string> Breaks => new()
    {
        { "in the middle of a paragraph", $"<w:p {W}><w:r><w:t>Before the break,</w:t></w:r><w:r><w:br w:type=\"page\"/></w:r><w:r><w:t>and after it.</w:t></w:r></w:p>" },
        { "in a paragraph of its own", $"<w:p {W}><w:r><w:br w:type=\"page\"/></w:r></w:p>" },
        { "a column break in a single column", $"<w:p {W}><w:r><w:t>Before the break,</w:t></w:r><w:r><w:br w:type=\"column\"/></w:r><w:r><w:t>and after it.</w:t></w:r></w:p>" },
    };

    [ComparedPdfTheory]
    [MemberData(nameof(Breaks))]
    public async Task A_page_break_starts_a_new_page_as_LibreOffice_starts_it(string kind, string paragraph)
    {
        var docx = Rendered(Letter(paragraph));
        var expected = PdfLines.Read(await new LibreOfficePdfConverter().ConvertAsync(docx));
        var actual = PdfLines.Read(await new HeliographPdfConverter().ConvertAsync(docx));

        PdfComparison.FirstDifference(expected, actual, "LibreOffice").Difference.Should().BeNull(kind);
        actual.Single(l => l.Text.StartsWith("The appendix", StringComparison.Ordinal)).Page.Should().Be(2, kind);
    }

    [Theory]
    [InlineData("<w:tbl {W}><w:tblGrid><w:gridCol w:w=\"4000\"/></w:tblGrid><w:tr><w:tc><w:p><w:r><w:br w:type=\"page\"/></w:r></w:p></w:tc></w:tr></w:tbl>")]
    public void A_page_break_in_a_table_is_refused_at_compile(string xml)
    {
        var docx = new TestDocx().RawParagraph(xml.Replace("{W}", W, StringComparison.Ordinal)).Build();

        DocumentTemplate.Compile(docx, NoFields, [new HeliographPdfConverter()]).Diagnostics
            .Should().ContainSingle(d => d.Code == DiagnosticCodes.PdfCannotLayOutSections).Which.Message.Should().Contain("page break in a table");
    }

    private static MemoryStream Letter(string breaking)
    {
        var styles = $"<w:styles {W}><w:docDefaults><w:rPrDefault><w:rPr><w:rFonts w:ascii=\"Arial\" w:hAnsi=\"Arial\"/><w:sz w:val=\"22\"/></w:rPr></w:rPrDefault>"
            + "<w:pPrDefault><w:pPr><w:spacing w:after=\"160\" w:line=\"259\" w:lineRule=\"auto\"/></w:pPr></w:pPrDefault></w:docDefaults></w:styles>";
        return new TestDocx().Part("word/styles.xml", styles, "application/vnd.openxmlformats-officedocument.wordprocessingml.styles+xml")
            .Relationship("http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles", "styles.xml", external: false)
            .RawParagraph($"<w:p {W}><w:r><w:t>Dear Ms Example, thank you for your letter.</w:t></w:r></w:p>")
            .RawParagraph(breaking)
            .RawParagraph($"<w:p {W}><w:r><w:t>The appendix, with the fees for the year.</w:t></w:r></w:p>").Build();
    }

    private static byte[] Rendered(MemoryStream template)
    {
        var compilation = DocumentTemplate.Compile(template, NoFields, [new HeliographPdfConverter()]);
        compilation.Diagnostics.Should().BeEmpty();
        return compilation.Document!.Render(JsonDocument.Parse("{}").RootElement, Benchmark.British).Docx!;
    }
}
