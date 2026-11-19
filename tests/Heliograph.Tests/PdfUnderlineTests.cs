using Heliograph.Documents;
using Heliograph.Pdf;
using Heliograph.Tests.Support;
using System.Text.Json;

namespace Heliograph.Tests;

/// <summary>
/// Underlines in Heliograph's own PDF converter, which drew every kind as one line in the text's colour: double, thick,
/// under the words only, and in a colour of their own; and what it refuses rather than draw wrongly.
/// </summary>
public class PdfUnderlineTests
{
    private const string W = "xmlns:w=\"http://schemas.openxmlformats.org/wordprocessingml/2006/main\"";
    private static readonly FieldDictionary NoFields = new([]);

    [PdfLinesFact]
    public async Task A_double_underline_is_two_lines()
    {
        var page = await Page("<w:u w:val=\"double\"/>");

        Bands(page, 120).Should().Be(2);
        Bands(await Page("<w:u w:val=\"single\"/>"), 120).Should().Be(1);
    }

    [PdfLinesFact]
    public async Task An_underline_under_the_words_only_leaves_the_spaces_alone()
    {
        var (words, all) = (await Page("<w:u w:val=\"words\"/>"), await Page("<w:u w:val=\"single\"/>"));

        // Between "Sample" and "Street" there is no line when only the words are underlined.
        Bands(words, Gap(all)).Should().Be(0);
        Bands(all, Gap(all)).Should().Be(1);
    }

    [PdfLinesFact]
    public async Task An_underline_in_a_colour_of_its_own_is_drawn_in_it()
    {
        var page = await Page("<w:u w:val=\"single\" w:color=\"C00000\"/>");

        Enumerable.Range(0, 40).Select(dy => page.At(120, Foot + dy / 4.0)).Should().Contain(c => c.R > 150 && c.G < 60 && c.B < 60);
    }

    [Theory]
    [InlineData("<w:r><w:rPr><w:u w:val=\"wave\"/></w:rPr><w:t>Wavy</w:t></w:r>", "wavy underline")]
    [InlineData("<w:r><w:rPr><w:bdr w:val=\"single\" w:sz=\"4\" w:space=\"0\" w:color=\"auto\"/></w:rPr><w:t>Boxed</w:t></w:r>", "border round text")]
    [InlineData("<m:oMath xmlns:m=\"http://schemas.openxmlformats.org/officeDocument/2006/math\"><m:r><m:t>x</m:t></m:r></m:oMath>", "equation")]
    public void An_underline_or_mark_it_cannot_draw_is_refused_at_compile(string runs, string named)
    {
        var docx = new TestDocx().RawParagraph($"<w:p {W}>{runs}</w:p>").Build();

        DocumentTemplate.Compile(docx, NoFields, [new HeliographPdfConverter()]).Diagnostics
            .Should().ContainSingle(d => d.Code == DiagnosticCodes.PdfCannotDrawFormatting).Which.Message.Should().Contain(named);
    }

    // Where the text's foot is: 36-point text from the top margin, its baseline about 33 points down.
    private const double Foot = 72 + 33;

    // How many separate dark bands cross a column just below the baseline.
    private static int Bands(PdfRender page, double x)
    {
        var dark = Enumerable.Range(0, 48).Select(dy => page.At(x, Foot + dy / 4.0)).Select(c => c.R < 128 && c.G < 128 && c.B < 128).ToList();
        return dark.Where((d, i) => d && (i == 0 || !dark[i - 1])).Count();
    }

    // The middle of the space between the two words: the widest gap between the letters' columns.
    private static double Gap(PdfRender page)
    {
        var columns = Enumerable.Range(72, 400).Where(x => Enumerable.Range(0, 30).Any(dy => page.At(x, 72 + dy) is { R: < 128 })).ToList();
        var widest = columns.Zip(columns.Skip(1)).MaxBy(p => p.Second - p.First);
        return (widest.First + widest.Second) / 2.0;
    }

    private static async Task<PdfRender> Page(string u)
    {
        var docx = new TestDocx().RawParagraph($"<w:p {W}><w:r><w:rPr><w:rFonts w:ascii=\"Arial\" w:hAnsi=\"Arial\"/><w:sz w:val=\"72\"/>{u}</w:rPr><w:t>Sample Street</w:t></w:r></w:p>").Build();
        var compilation = DocumentTemplate.Compile(docx, NoFields, [new HeliographPdfConverter()]);
        var rendered = compilation.Document!.Render(JsonDocument.Parse("{}").RootElement, Benchmark.British).Docx!;
        return PdfRender.FirstPage(await new HeliographPdfConverter().ConvertAsync(rendered));
    }
}
