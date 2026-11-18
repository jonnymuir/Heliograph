using System.Text.Json;
using Heliograph.Documents;
using Heliograph.Pdf;
using Heliograph.Pdf.LibreOffice;
using Heliograph.Tests.Support;

namespace Heliograph.Tests;

/// <summary>
/// Borders and shading round paragraphs in Heliograph's own PDF converter: a boxed notice, paragraphs sharing one box with
/// or without a line between them, laid out as LibreOffice lays them out.
/// </summary>
public class PdfParagraphBoxTests
{
    private const string W = "xmlns:w=\"http://schemas.openxmlformats.org/wordprocessingml/2006/main\"";
    private const string Box = "<w:pBdr><w:top w:val=\"single\" w:sz=\"12\" w:space=\"4\" w:color=\"auto\"/><w:left w:val=\"single\" w:sz=\"12\" w:space=\"4\" w:color=\"auto\"/>"
        + "<w:bottom w:val=\"single\" w:sz=\"12\" w:space=\"4\" w:color=\"auto\"/><w:right w:val=\"single\" w:sz=\"12\" w:space=\"4\" w:color=\"auto\"/></w:pBdr>";
    private const string Between = "<w:pBdr><w:top w:val=\"single\" w:sz=\"4\" w:space=\"1\" w:color=\"auto\"/><w:bottom w:val=\"single\" w:sz=\"4\" w:space=\"1\" w:color=\"auto\"/>"
        + "<w:between w:val=\"single\" w:sz=\"4\" w:space=\"1\" w:color=\"auto\"/></w:pBdr>";
    private const string Shaded = "<w:shd w:val=\"clear\" w:color=\"auto\" w:fill=\"FFF2CC\"/>";
    private static readonly FieldDictionary NoFields = new([]);

    private const string Words = "Please read this before you renew: membership now runs from April to March, and fees paid after the end of April carry a late charge.";

    public static TheoryData<string, string, int> Boxes => new()
    {
        { "one boxed paragraph", Box, 1 },
        { "two paragraphs in one box", Box, 2 },
        { "a line between paragraphs", Between, 3 },
        { "shading", Shaded, 2 },
        { "shading in a box", Box + Shaded, 2 },
    };

    [ComparedPdfTheory]
    [MemberData(nameof(Boxes))]
    public async Task A_paragraphs_box_is_laid_out_as_LibreOffice_lays_it_out(string kind, string pPr, int boxed)
    {
        var docx = Rendered(Letter(pPr, boxed));
        var expected = PdfLines.Read(await new LibreOfficePdfConverter().ConvertAsync(docx));
        var actual = PdfLines.Read(await new HeliographPdfConverter().ConvertAsync(docx));

        actual.Select(l => l.Text).Should().Equal(expected.Select(l => l.Text), kind);
        actual.Zip(expected).Should().OnlyContain(pair => Math.Abs(pair.First.Top - pair.Second.Top) <= 1.5, kind);
    }

    [PdfLinesFact]
    public async Task A_box_and_its_shading_are_drawn()
    {
        var pdf = await new HeliographPdfConverter().ConvertAsync(Rendered(Letter(Box + Shaded, 1)));
        var (page, line) = (PdfRender.FirstPage(pdf), PdfLines.Read(pdf).First(l => l.Text.StartsWith("Please", StringComparison.Ordinal)));

        page.At(line.Left + 100, line.Top - 2).Should().Be((255, 242, 204), "the shading is behind the text");
        Enumerable.Range(1, 8).Select(dx => page.At(line.Left - dx, line.Top + 4)).Should().Contain(c => c.R < 100, "the left border is drawn");
    }

    // Plain paragraphs round some that share a frame.
    private static MemoryStream Letter(string pPr, int boxed)
    {
        var styles = $"<w:styles {W}><w:docDefaults><w:rPrDefault><w:rPr><w:rFonts w:ascii=\"Arial\" w:hAnsi=\"Arial\"/><w:sz w:val=\"22\"/></w:rPr></w:rPrDefault>"
            + "<w:pPrDefault><w:pPr><w:spacing w:after=\"160\"/></w:pPr></w:pPrDefault></w:docDefaults></w:styles>";
        var docx = new TestDocx().Part("word/styles.xml", styles, "application/vnd.openxmlformats-officedocument.wordprocessingml.styles+xml")
            .Relationship("http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles", "styles.xml", external: false);
        docx.RawParagraph($"<w:p {W}><w:r><w:t>Dear Ms Example, thank you for your letter.</w:t></w:r></w:p>");
        for (var k = 0; k < boxed; k++)
        {
            docx.RawParagraph($"<w:p {W}><w:pPr>{pPr}<w:ind w:left=\"360\" w:right=\"360\"/></w:pPr><w:r><w:t>{Words}</w:t></w:r></w:p>");
        }

        docx.RawParagraph($"<w:p {W}><w:r><w:t>We look forward to seeing you at the next meeting.</w:t></w:r></w:p>");
        return docx.Build();
    }

    private static byte[] Rendered(MemoryStream template)
    {
        var compilation = DocumentTemplate.Compile(template, NoFields, [new HeliographPdfConverter()]);
        compilation.Diagnostics.Should().BeEmpty();
        return compilation.Document!.Render(JsonDocument.Parse("{}").RootElement, Benchmark.British).Docx!;
    }
}
