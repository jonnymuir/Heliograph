using System.Text.Json;
using Heliograph.Documents;
using Heliograph.Pdf;
using Heliograph.Pdf.LibreOffice;
using Heliograph.Tests.Support;

namespace Heliograph.Tests;

/// <summary>
/// How text is set in Heliograph's own PDF converter, beyond bold, italic and underline: colour, highlight and shading,
/// strikethrough, superscript and subscript, capitals and character spacing, laid out as LibreOffice lays them out; and
/// hidden text, which is not drawn.
/// </summary>
public class PdfTextTests
{
    private const string W = "xmlns:w=\"http://schemas.openxmlformats.org/wordprocessingml/2006/main\"";
    private static readonly FieldDictionary NoFields = new([]);

    private const string Words = "Thank you for your application to the National Juggling Federation, which we received on 3 March and will answer soon.";

    public static TheoryData<string, string> Formatting => new()
    {
        { "colour", "<w:color w:val=\"C00000\"/>" },
        { "highlight", "<w:highlight w:val=\"yellow\"/>" },
        { "shading", "<w:shd w:val=\"clear\" w:color=\"auto\" w:fill=\"D9E2F3\"/>" },
        { "strikethrough", "<w:strike/>" },
        { "double strikethrough", "<w:dstrike/>" },
        { "superscript", "<w:vertAlign w:val=\"superscript\"/>" },
        { "subscript", "<w:vertAlign w:val=\"subscript\"/>" },
        { "capitals", "<w:caps/>" },
        { "expanded", "<w:spacing w:val=\"40\"/>" },
        { "condensed", "<w:spacing w:val=\"-10\"/>" },
    };

    [ComparedPdfTheory]
    [MemberData(nameof(Formatting))]
    public async Task Formatted_text_is_laid_out_as_LibreOffice_lays_it_out(string kind, string rPr)
    {
        var docx = Rendered(Letter(rPr));
        var expected = PdfLines.Read(await new LibreOfficePdfConverter().ConvertAsync(docx));
        var actual = PdfLines.Read(await new HeliographPdfConverter().ConvertAsync(docx));

        actual.Select(l => l.Text).Should().Equal(expected.Select(l => l.Text), kind);
        actual.Zip(expected).Should().OnlyContain(pair => Math.Abs(pair.First.Top - pair.Second.Top) <= 1.5, kind);
    }

    [PdfLinesFact]
    public async Task Coloured_text_is_drawn_in_its_colour_on_its_highlight()
    {
        var page = PdfRender.FirstPage(await new HeliographPdfConverter().ConvertAsync(
            Rendered(Letter("<w:color w:val=\"C00000\"/><w:highlight w:val=\"yellow\"/><w:sz w:val=\"72\"/>", "MMMM"))));

        var colours = Enumerable.Range(72, 60).SelectMany(y => Enumerable.Range(72, 150).Select(x => page.At(x, y))).ToHashSet();
        colours.Should().Contain((255, 255, 0), "the highlight is drawn behind the text");
        colours.Should().Contain(c => c.R > 150 && c.G < 60 && c.B < 60, "the text is drawn in dark red");
    }

    [PdfLinesFact]
    public async Task Hidden_text_is_not_drawn()
    {
        var docx = Rendered(Paragraphs($"<w:r><w:t xml:space=\"preserve\">Shown </w:t></w:r><w:r><w:rPr><w:vanish/></w:rPr><w:t>hidden</w:t></w:r><w:r><w:t xml:space=\"preserve\"> also shown</w:t></w:r>"), allowWarnings: true);

        PdfLines.Read(await new HeliographPdfConverter().ConvertAsync(docx)).Select(l => l.Text).Should().Equal("Shown also shown");
    }

    [VeraPdfTheory]
    [InlineData("<w:color w:val=\"C00000\"/><w:highlight w:val=\"yellow\"/><w:strike/>")]
    [InlineData("<w:vertAlign w:val=\"superscript\"/><w:caps/><w:spacing w:val=\"20\"/>")]
    public async Task Formatted_text_is_still_an_accessible_PDF(string rPr) =>
        PdfValidator.FailuresAsPdfUa(await new HeliographPdfConverter().ConvertAsync(Rendered(Letter(rPr)))).Should().BeEmpty();

    // Plain text with a formatted run in the middle of each paragraph.
    private static MemoryStream Letter(string rPr, string formatted = "application to the National Juggling Federation")
    {
        var run = $"<w:r><w:t xml:space=\"preserve\">Thank you for your </w:t></w:r><w:r><w:rPr>{rPr}</w:rPr><w:t>{formatted}</w:t></w:r>"
            + "<w:r><w:t xml:space=\"preserve\">, which we received on 3 March and will answer soon. </w:t></w:r>"
            + $"<w:r><w:t>{Words} {Words}</w:t></w:r>";
        return Paragraphs(run, run);
    }

    private static MemoryStream Paragraphs(params string[] runs)
    {
        var styles = $"<w:styles {W}><w:docDefaults><w:rPrDefault><w:rPr><w:rFonts w:ascii=\"Arial\" w:hAnsi=\"Arial\"/><w:sz w:val=\"22\"/></w:rPr></w:rPrDefault>"
            + "<w:pPrDefault><w:pPr><w:spacing w:after=\"160\"/></w:pPr></w:pPrDefault></w:docDefaults></w:styles>";
        var docx = new TestDocx().Part("word/styles.xml", styles, "application/vnd.openxmlformats-officedocument.wordprocessingml.styles+xml")
            .Relationship("http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles", "styles.xml", external: false);
        foreach (var run in runs)
        {
            docx.RawParagraph($"<w:p {W}>{run}</w:p>");
        }

        return docx.Build();
    }

    private static byte[] Rendered(MemoryStream template, bool allowWarnings = false)
    {
        var compilation = DocumentTemplate.Compile(template, NoFields, [new HeliographPdfConverter()]);
        if (!allowWarnings)
        {
            compilation.Diagnostics.Should().BeEmpty();
        }

        return compilation.Document!.Render(JsonDocument.Parse("{}").RootElement, Benchmark.British).Docx!;
    }
}
