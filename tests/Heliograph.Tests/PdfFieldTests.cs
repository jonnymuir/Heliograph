using System.Text.Json;
using Heliograph.Documents;
using Heliograph.Pdf;
using Heliograph.Pdf.LibreOffice;
using Heliograph.Tests.Support;

namespace Heliograph.Tests;

/// <summary>
/// Page fields in Heliograph's own PDF converter: the page number, the count of pages in the letter and in its section,
/// each in the format its switch gives it, written as Word writes complex fields, an instruction split across runs.
/// </summary>
/// <remarks>
/// LibreOffice is no guide here: it prints a section's page count as the result last saved, and counts in the letter a
/// blank page it would add so a page numbered 1 again falls on the right, which its PDF leaves out. Word's own layout of
/// the same letter (WordProbes, probe-page-fields) is the judge.
/// </remarks>
public class PdfFieldTests
{
    private const string W = "xmlns:w=\"http://schemas.openxmlformats.org/wordprocessingml/2006/main\"";
    private static readonly FieldDictionary NoFields = new([]);

    [PdfLinesFact]
    public async Task Each_page_has_its_number_and_counts_in_their_formats()
    {
        var lines = PdfLines.Read(await new HeliographPdfConverter().ConvertAsync(Rendered(FieldLetters.Letter())));
        var footers = Enumerable.Range(1, lines.Max(l => l.Page)).Select(p => lines.Last(l => l.Page == p).Text).ToList();

        footers.Should().Equal("Page 1 of 3 (4 in all)", "Page 2 of 3 (4 in all)", "Page 3 of 3 (4 in all)", "Appendix page I of 1");
    }

    [Theory]
    [InlineData(" DATE \\@ \"d MMMM yyyy\" ", "DATE field")]
    [InlineData(" PAGE \\* Ordinal ", "switch \\* Ordinal")]
    [InlineData(" NUMPAGES \\# \"00\" ", "switch \\#")]
    public void A_field_it_cannot_work_out_is_refused_at_compile(string instruction, string named)
    {
        var docx = new TestDocx().RawParagraph($"<w:p {W}>{FieldLetters.Field(instruction)}</w:p>").Build();

        DocumentTemplate.Compile(docx, NoFields, [new HeliographPdfConverter()]).Diagnostics
            .Should().ContainSingle(d => d.Code == DiagnosticCodes.PdfCannotWorkOutField).Which.Message.Should().Contain(named);
    }

    [Fact]
    public void A_page_field_split_across_runs_is_not_refused()
    {
        var docx = new TestDocx().RawParagraph($"<w:p {W}>{FieldLetters.Field(" PAGE ", " \\* ROMAN ", " \\* MERGEFORMAT ")}</w:p>").Build();

        DocumentTemplate.Compile(docx, NoFields, [new HeliographPdfConverter()]).Diagnostics.Should().BeEmpty();
    }

    private static byte[] Rendered(MemoryStream template)
    {
        var compilation = DocumentTemplate.Compile(template, NoFields, [new HeliographPdfConverter()]);
        compilation.Diagnostics.Should().BeEmpty();
        return compilation.Document!.Render(JsonDocument.Parse("{}").RootElement, Benchmark.British).Docx!;
    }
}
