using System.Text.Json;
using Heliograph.Documents;
using Heliograph.Pdf;
using Heliograph.Pdf.LibreOffice;
using Heliograph.Tests.Support;

namespace Heliograph.Tests;

/// <summary>
/// Columns in Heliograph's own PDF converter: text flowing down each column and on to the next page, columns of equal
/// width or each of its own, a paragraph broken again where it goes on in a column of another width, a column break, a page break, and a section in columns after one that is not, laid out as
/// LibreOffice lays them out; a line between the columns when the section asks.
/// </summary>
public class PdfColumnTests
{
    private const string W = "xmlns:w=\"http://schemas.openxmlformats.org/wordprocessingml/2006/main\"";
    private const string Words = "Thank you for your application to the National Juggling Federation, which we received on 3 March.";
    private static readonly FieldDictionary NoFields = new([]);

    private const string Two = "<w:cols w:num=\"2\" w:space=\"720\"/>";
    internal const string Three = "<w:cols w:num=\"3\" w:sep=\"1\" w:space=\"360\"/>";
    internal const string Uneven = "<w:cols w:num=\"2\" w:space=\"360\" w:equalWidth=\"0\"><w:col w:w=\"3000\" w:space=\"360\"/><w:col w:w=\"6000\"/></w:cols>";
    private const string Wider = "<w:cols w:num=\"2\" w:space=\"360\" w:equalWidth=\"0\"><w:col w:w=\"6000\" w:space=\"360\"/><w:col w:w=\"3000\"/></w:cols>";

    public static TheoryData<string, string, string> Letters => new()
    {
        { "two columns of equal width", Two, "" },
        { "three columns with lines between", Three, "" },
        { "a column break", Two, "<w:p><w:r><w:t>Before the break,</w:t></w:r><w:r><w:br w:type=\"column\"/></w:r><w:r><w:t>and after it.</w:t></w:r></w:p>" },
        { "a narrow column, then a wide one", Uneven, "" },
        { "a wide column, then a narrow one", Wider, "" },
        { "a page break in columns", Two, "<w:p><w:r><w:t>Before the break,</w:t></w:r><w:r><w:br w:type=\"page\"/></w:r><w:r><w:t>and after it.</w:t></w:r></w:p>" },
    };

    [ComparedPdfTheory]
    [MemberData(nameof(Letters))]
    public async Task Columns_are_laid_out_as_LibreOffice_lays_them_out(string kind, string cols, string extra)
    {
        var docx = Rendered(Letter(cols, extra, before: false));
        var expected = PdfLines.Read(await new LibreOfficePdfConverter().ConvertAsync(docx));
        var actual = PdfLines.Read(await new HeliographPdfConverter().ConvertAsync(docx));

        PdfComparison.FirstDifference(expected, actual, "LibreOffice").Difference.Should().BeNull(kind);
    }

    [ComparedPdfFact]
    public async Task A_section_in_columns_after_one_that_is_not_is_laid_out_as_LibreOffice_lays_it_out()
    {
        var docx = Rendered(Letter(Two, "", before: true));
        var expected = PdfLines.Read(await new LibreOfficePdfConverter().ConvertAsync(docx));
        var actual = PdfLines.Read(await new HeliographPdfConverter().ConvertAsync(docx));

        PdfComparison.FirstDifference(expected, actual, "LibreOffice").Difference.Should().BeNull();
    }

    [PdfLinesFact]
    public async Task A_line_runs_between_columns_when_the_section_asks()
    {
        var page = PdfRender.FirstPage(await new HeliographPdfConverter().ConvertAsync(Rendered(Letter(Three, "", before: false))));

        // Three columns across 468 points with 18 between are 144 wide, so the first line is 144 + 9 in from the margin.
        Enumerable.Range(-2, 5).Select(dx => page.At(72 + 144 + 9 + dx, 300)).Should().Contain(c => c.R < 128);
    }

    [Fact]
    public void Footnotes_in_a_letter_set_in_columns_are_refused_at_compile()
    {
        var docx = NoteLetters.Letter([NoteLetters.Referring("Your membership renews in April.", 1), $"<w:sectPr {W}>{Two}</w:sectPr>"], "Membership runs from April to March.");

        DocumentTemplate.Compile(docx, NoFields, [new HeliographPdfConverter()]).Diagnostics
            .Should().ContainSingle(d => d.Code == DiagnosticCodes.PdfCannotLayOutColumns).Which.Message.Should().Contain("footnotes");
    }

    // Enough paragraphs to fill the columns of a page and run over, in a section with these columns; before them, when
    // asked, a section of one column.
    internal static MemoryStream Letter(string cols, string extra, bool before)
    {
        var styles = $"<w:styles {W}><w:docDefaults><w:rPrDefault><w:rPr><w:rFonts w:ascii=\"Arial\" w:hAnsi=\"Arial\"/><w:sz w:val=\"22\"/></w:rPr></w:rPrDefault>"
            + "<w:pPrDefault><w:pPr><w:spacing w:after=\"160\" w:line=\"259\" w:lineRule=\"auto\"/></w:pPr></w:pPrDefault></w:docDefaults></w:styles>";
        var docx = new TestDocx().Part("word/styles.xml", styles, "application/vnd.openxmlformats-officedocument.wordprocessingml.styles+xml")
            .Relationship("http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles", "styles.xml", external: false);
        if (before)
        {
            docx.RawParagraph($"<w:p {W}><w:r><w:t>An introduction in one column.</w:t></w:r></w:p>");
            docx.RawParagraph($"<w:p {W}><w:pPr><w:sectPr/></w:pPr><w:r><w:t>{Words}</w:t></w:r></w:p>");
        }

        for (var i = 0; i < 30; i++)
        {
            docx.RawParagraph($"<w:p {W}><w:r><w:t>{i}. {Words}</w:t></w:r></w:p>");
            if (i == 5 && extra.Length > 0)
            {
                docx.RawParagraph(extra.Replace("<w:p>", $"<w:p {W}>", StringComparison.Ordinal));
            }
        }

        docx.RawParagraph($"<w:sectPr {W}>{cols}</w:sectPr>");
        return docx.Build();
    }

    private static byte[] Rendered(MemoryStream template)
    {
        var compilation = DocumentTemplate.Compile(template, NoFields, [new HeliographPdfConverter()]);
        compilation.Diagnostics.Should().BeEmpty();
        return compilation.Document!.Render(JsonDocument.Parse("{}").RootElement, Benchmark.British).Docx!;
    }
}
