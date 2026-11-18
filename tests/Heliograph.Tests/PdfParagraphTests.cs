using System.Text.Json;
using Heliograph.Documents;
using Heliograph.Pdf;
using Heliograph.Pdf.LibreOffice;
using Heliograph.Tests.Support;

namespace Heliograph.Tests;

/// <summary>
/// Rules about the space between paragraphs in Heliograph's own PDF converter: none between paragraphs of the same style
/// when they say so, as in Word's List Paragraph; and what it cannot do yet refused at compile.
/// </summary>
public class PdfParagraphTests
{
    private const string W = "xmlns:w=\"http://schemas.openxmlformats.org/wordprocessingml/2006/main\"";
    private static readonly FieldDictionary NoFields = new([]);

    [ComparedPdfTheory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Paragraphs_of_the_same_style_leave_out_the_space_between_them_when_they_say_so(bool newWordDocument)
    {
        var docx = Rendered(Letter(newWordDocument));
        var expected = PdfLines.Read(await new LibreOfficePdfConverter().ConvertAsync(docx));
        var actual = PdfLines.Read(await new HeliographPdfConverter().ConvertAsync(docx));

        actual.Select(l => l.Text).Should().Equal(expected.Select(l => l.Text));
        actual.Zip(expected).Should().OnlyContain(pair => Math.Abs(pair.First.Top - pair.Second.Top) <= 1.5);
    }

    [Theory]
    [InlineData("<w:autoHyphenation/>", "", "hyphenates")]
    [InlineData("", "<w:spacing w:beforeAutospacing=\"1\" w:afterAutospacing=\"1\"/>", "automatic")]
    public void Spacing_and_hyphenation_it_cannot_lay_out_are_refused_at_compile(string setting, string pPr, string named)
    {
        var docx = new TestDocx().Part("word/settings.xml", $"<w:settings {W}>{setting}</w:settings>", "application/vnd.openxmlformats-officedocument.wordprocessingml.settings+xml")
            .Relationship("http://schemas.openxmlformats.org/officeDocument/2006/relationships/settings", "settings.xml", external: false)
            .RawParagraph($"<w:p {W}><w:pPr>{pPr}</w:pPr><w:r><w:t>Dear Ms Example</w:t></w:r></w:p>").Build();

        DocumentTemplate.Compile(docx, NoFields, [new HeliographPdfConverter()]).Diagnostics
            .Should().Contain(d => d.Code == DiagnosticCodes.PdfCannotDrawFormatting && d.Message.Contains(named, StringComparison.Ordinal));
    }

    // Body text with space after, then items in a List Paragraph style that leaves out space between its own paragraphs.
    private static MemoryStream Letter(bool newWordDocument)
    {
        var styles = $"<w:styles {W}><w:docDefaults><w:rPrDefault><w:rPr><w:rFonts w:ascii=\"Arial\" w:hAnsi=\"Arial\"/><w:sz w:val=\"22\"/></w:rPr></w:rPrDefault>"
            + "<w:pPrDefault><w:pPr><w:spacing w:after=\"160\" w:line=\"259\" w:lineRule=\"auto\"/></w:pPr></w:pPrDefault></w:docDefaults>"
            + "<w:style w:type=\"paragraph\" w:default=\"1\" w:styleId=\"Normal\"><w:name w:val=\"Normal\"/></w:style>"
            + "<w:style w:type=\"paragraph\" w:styleId=\"ListParagraph\"><w:name w:val=\"List Paragraph\"/><w:basedOn w:val=\"Normal\"/>"
            + "<w:pPr><w:spacing w:before=\"120\"/><w:ind w:left=\"720\"/><w:contextualSpacing/></w:pPr></w:style></w:styles>";
        var compat = newWordDocument ? "" : "<w:compat><w:doNotUseHTMLParagraphAutoSpacing/></w:compat>";
        var docx = new TestDocx().Part("word/styles.xml", styles, "application/vnd.openxmlformats-officedocument.wordprocessingml.styles+xml")
            .Relationship("http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles", "styles.xml", external: false)
            .Part("word/settings.xml", $"<w:settings {W}>{compat}</w:settings>", "application/vnd.openxmlformats-officedocument.wordprocessingml.settings+xml")
            .Relationship("http://schemas.openxmlformats.org/officeDocument/2006/relationships/settings", "settings.xml", external: false);
        docx.RawParagraph($"<w:p {W}><w:r><w:t>Dear Ms Example, here is what your membership includes.</w:t></w:r></w:p>");
        foreach (var item in new[] { "Entry to every meeting", "The quarterly newsletter", "Insurance at events" })
        {
            docx.RawParagraph($"<w:p {W}><w:pPr><w:pStyle w:val=\"ListParagraph\"/></w:pPr><w:r><w:t>{item}</w:t></w:r></w:p>");
        }

        docx.RawParagraph($"<w:p {W}><w:r><w:t>We look forward to seeing you.</w:t></w:r></w:p>");
        return docx.Build();
    }

    private static byte[] Rendered(MemoryStream template)
    {
        var compilation = DocumentTemplate.Compile(template, NoFields, [new HeliographPdfConverter()]);
        compilation.Diagnostics.Should().BeEmpty();
        return compilation.Document!.Render(JsonDocument.Parse("{}").RootElement, Benchmark.British).Docx!;
    }
}
