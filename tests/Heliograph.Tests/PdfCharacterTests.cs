using System.Text;
using System.Text.Json;
using Heliograph.Documents;
using Heliograph.Pdf;
using Heliograph.Tests.Support;

namespace Heliograph.Tests;

/// <summary>
/// Characters in Heliograph's own PDF converter: one the run's font lacks is drawn from the next built-in font that has it,
/// and one no built-in font has is refused in a template and fails the conversion, without quoting it, in a recipient's data.
/// </summary>
public class PdfCharacterTests
{
    private const string W = "xmlns:w=\"http://schemas.openxmlformats.org/wordprocessingml/2006/main\"";
    private static readonly FieldDictionary Dictionary = new([new Field("Member name", "memberName", FieldType.Text)]);

    [PdfLinesTheory]
    [InlineData("Cambria", "Ζωή Οικονόμου and Жанна Иванова", "Liberation")]
    [InlineData("Arial", "a fee of ₹450 a year", "Carlito")]
    public async Task A_character_its_font_lacks_is_drawn_from_a_built_in_font_that_has_it(string font, string text, string fallback)
    {
        var pdf = await Convert(Docx(font, text), "{}");

        PdfLines.Read(pdf).Select(l => l.Text).Should().ContainSingle().Which.Should().Be(text);
        Encoding.Latin1.GetString(pdf).Should().Contain($"+{fallback}");
    }

    [Fact]
    public async Task A_name_no_built_in_font_can_draw_fails_the_conversion_without_quoting_it()
    {
        var act = () => Convert(Docx("Arial", "Dear ((Member name))"), """{ "memberName": "李明" }""");

        (await act.Should().ThrowAsync<PdfConversionException>()).Which.Message.Should().NotContain("李明").And.Contain("character");
    }

    [Theory]
    [InlineData("Our office in 北京")]
    [InlineData("See you soon 😀")]
    public void A_character_no_built_in_font_can_draw_is_refused_at_compile(string text)
    {
        var compilation = DocumentTemplate.Compile(Docx("Arial", text), Dictionary, [new HeliographPdfConverter()]);

        compilation.Diagnostics.Should().ContainSingle(d => d.Code == DiagnosticCodes.PdfCannotDrawCharacter).Which.Location!.Paragraph.Should().Be(1);
        compilation.Succeeded.Should().BeFalse();
    }

    private static MemoryStream Docx(string font, string text) => new TestDocx()
        .RawParagraph($"<w:p {W}><w:r><w:rPr><w:rFonts w:ascii=\"{font}\" w:hAnsi=\"{font}\"/></w:rPr><w:t>{text}</w:t></w:r></w:p>").Build();

    private static async Task<byte[]> Convert(MemoryStream template, string data)
    {
        var compilation = DocumentTemplate.Compile(template, Dictionary);
        var docx = compilation.Document!.Render(JsonDocument.Parse(data).RootElement, Benchmark.British).Docx!;
        return await new HeliographPdfConverter().ConvertAsync(docx);
    }
}
