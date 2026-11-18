using System.Text.Json;
using Heliograph.Documents;
using Heliograph.Pdf;
using Heliograph.Pdf.LibreOffice;
using Heliograph.Tests.Support;

namespace Heliograph.Tests;

/// <summary>
/// Fonts in Heliograph's own PDF converter: Calibri and Cambria drawn in Carlito and Caladea, the look-alikes LibreOffice
/// uses, whether a document names them or takes them from its theme; and a font with no look-alike pointed out.
/// </summary>
public class PdfFontTests
{
    private const string W = "xmlns:w=\"http://schemas.openxmlformats.org/wordprocessingml/2006/main\"";
    private const string Rel = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
    private static readonly FieldDictionary NoFields = new([]);

    private static readonly string Text = string.Join(" ", Enumerable.Repeat(
        "Dear Ms Example, thank you for your application to the National Juggling Federation, which we received on 3 March.", 6));

    [ComparedPdfTheory]
    [InlineData("Calibri", false, false)]
    [InlineData("Cambria", false, false)]
    [InlineData("Calibri", true, false)]
    [InlineData("Cambria", true, true)]
    public async Task A_font_with_a_look_alike_is_laid_out_as_LibreOffice_lays_it_out(string font, bool fromTheme, bool bold)
    {
        var docx = Rendered(Docx(font, fromTheme, bold));
        var expected = PdfLines.Read(await new LibreOfficePdfConverter().ConvertAsync(docx));
        var actual = PdfLines.Read(await new HeliographPdfConverter().ConvertAsync(docx));

        actual.Select(l => l.Text).Should().Equal(expected.Select(l => l.Text));
        actual.Zip(expected).Should().OnlyContain(pair => Math.Abs(pair.First.Top - pair.Second.Top) <= 1.5);
    }

    [Theory]
    [InlineData("Aptos", true, true)]
    [InlineData("Garamond", false, true)]
    [InlineData("Calibri", true, false)]
    [InlineData("Cambria", false, false)]
    public void A_font_with_no_look_alike_is_a_warning_wherever_it_is_set(string font, bool fromTheme, bool warned)
    {
        var compilation = DocumentTemplate.Compile(Docx(font, fromTheme, bold: false), NoFields, [new HeliographPdfConverter()]);

        compilation.Succeeded.Should().BeTrue();
        var warnings = compilation.Diagnostics.Where(d => d.Code == DiagnosticCodes.PdfFontDrawnInLookAlike).ToList();
        warnings.Should().HaveCount(warned ? 1 : 0);
        warnings.Should().OnlyContain(d => d.Message.Contains(font, StringComparison.Ordinal) && d.Severity == DiagnosticSeverity.Warning);
    }

    [Fact]
    public void A_style_no_text_uses_is_not_pointed_out()
    {
        var compilation = DocumentTemplate.Compile(Docx("Calibri", fromTheme: true, bold: false, majorFont: "Calibri Light"), NoFields, [new HeliographPdfConverter()]);

        compilation.Diagnostics.Should().NotContain(d => d.Code == DiagnosticCodes.PdfFontDrawnInLookAlike);
    }

    // A letter in one font at 11pt, named in the styles or taken from the theme's minor font. Heading 1 takes the major font
    // and no paragraph uses it.
    private static MemoryStream Docx(string font, bool fromTheme, bool bold, string majorFont = "Calibri Light")
    {
        var fonts = fromTheme ? "<w:rFonts w:asciiTheme=\"minorHAnsi\" w:hAnsiTheme=\"minorHAnsi\"/>" : $"<w:rFonts w:ascii=\"{font}\" w:hAnsi=\"{font}\"/>";
        var styles = $"<w:styles {W}><w:docDefaults><w:rPrDefault><w:rPr>{fonts}<w:sz w:val=\"22\"/></w:rPr></w:rPrDefault>"
            + "<w:pPrDefault><w:pPr><w:spacing w:after=\"160\" w:line=\"259\" w:lineRule=\"auto\"/></w:pPr></w:pPrDefault></w:docDefaults>"
            + "<w:style w:type=\"paragraph\" w:default=\"1\" w:styleId=\"Normal\"><w:name w:val=\"Normal\"/></w:style>"
            + "<w:style w:type=\"paragraph\" w:styleId=\"Heading1\"><w:name w:val=\"heading 1\"/><w:basedOn w:val=\"Normal\"/><w:rPr><w:rFonts w:asciiTheme=\"majorHAnsi\" w:hAnsiTheme=\"majorHAnsi\"/><w:sz w:val=\"32\"/></w:rPr></w:style></w:styles>";
        var docx = new TestDocx()
            .Part("word/styles.xml", styles, "application/vnd.openxmlformats-officedocument.wordprocessingml.styles+xml")
            .Relationship($"{Rel}/styles", "styles.xml", external: false)
            .Part("word/theme/theme1.xml", TestTheme.Xml(fromTheme ? font : "Arial", majorFont), "application/vnd.openxmlformats-officedocument.theme+xml")
            .Relationship($"{Rel}/theme", "theme/theme1.xml", external: false);
        var run = bold ? "<w:rPr><w:b/></w:rPr>" : "";
        docx.RawParagraph($"<w:p {W}><w:r>{run}<w:t>{Text}</w:t></w:r></w:p>");
        docx.RawParagraph($"<w:p {W}><w:r>{run}<w:t>{Text}</w:t></w:r></w:p>");
        return docx.Build();
    }

    private static byte[] Rendered(MemoryStream template)
    {
        var compilation = DocumentTemplate.Compile(template, NoFields, [new HeliographPdfConverter()]);
        compilation.Diagnostics.Should().BeEmpty();
        return compilation.Document!.Render(JsonDocument.Parse("{}").RootElement, Benchmark.British).Docx!;
    }
}
