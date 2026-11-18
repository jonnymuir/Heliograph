using System.Text;
using System.Text.Json;
using Heliograph.Documents;
using Heliograph.Pdf;
using Heliograph.Pdf.LibreOffice;
using Heliograph.Tests.Support;

namespace Heliograph.Tests;

/// <summary>
/// Links in Heliograph's own PDF converter: text in a link to a web page, an email address or a telephone number goes
/// there when clicked, and a screen reader knows it as a link. Any other link is refused at compile.
/// </summary>
public class PdfLinkTests
{
    private const string W = "xmlns:w=\"http://schemas.openxmlformats.org/wordprocessingml/2006/main\" xmlns:r=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships\"";
    private const string Hyperlink = "http://schemas.openxmlformats.org/officeDocument/2006/relationships/hyperlink";
    private static readonly FieldDictionary NoFields = new([]);

    [Theory]
    [InlineData("https://www.example.org/membership")]
    [InlineData("mailto:help@example.org")]
    [InlineData("tel:+441234567890")]
    public async Task A_link_goes_to_its_address_when_clicked(string address)
    {
        var pdf = Encoding.Latin1.GetString(await Pdf(Docx(address)));

        pdf.Should().Contain("/Subtype /Link").And.Contain($"/URI ({address})").And.Contain("/S /Link").And.Contain("/Type /OBJR");
    }

    [PdfLinesFact]
    public async Task A_links_area_covers_its_text()
    {
        var pdf = await Pdf(Docx("https://www.example.org/membership"));
        var rect = System.Text.RegularExpressions.Regex.Match(Encoding.Latin1.GetString(pdf), @"/Rect \[([\d.]+) [\d.]+ ([\d.]+)").Groups;
        var line = PdfLines.Read(pdf).Single(l => l.Text.Contains("membership page", StringComparison.Ordinal));

        double.Parse(rect[1].Value, System.Globalization.CultureInfo.InvariantCulture).Should().BeGreaterThan(line.Left + 50, "the link starts after the words before it");
        double.Parse(rect[2].Value, System.Globalization.CultureInfo.InvariantCulture).Should().BeLessThan(500, "the link ends with its words");
    }

    [ComparedPdfFact]
    public async Task Text_in_a_link_is_laid_out_as_LibreOffice_lays_it_out()
    {
        var docx = Rendered(Docx("https://www.example.org/membership"));
        var expected = PdfLines.Read(await new LibreOfficePdfConverter().ConvertAsync(docx));
        var actual = PdfLines.Read(await new HeliographPdfConverter().ConvertAsync(docx));

        actual.Select(l => l.Text).Should().Equal(expected.Select(l => l.Text));
        actual.Zip(expected).Should().OnlyContain(pair => Math.Abs(pair.First.Top - pair.Second.Top) <= 1.5);
    }

    [VeraPdfTheory]
    [InlineData("https://www.example.org/membership")]
    public async Task A_link_is_still_an_accessible_PDF(string address) =>
        PdfValidator.FailuresAsPdfUa(await Pdf(Docx(address))).Should().BeEmpty();

    [Theory]
    [InlineData("javascript:alert(1)", "not a web page")]
    [InlineData("file:///C:/letters/secret.docx", "not a web page")]
    public void A_link_to_anything_else_is_refused_at_compile(string address, string named)
    {
        var compilation = DocumentTemplate.Compile(Docx(address), NoFields, [new HeliographPdfConverter()]);

        compilation.Diagnostics.Should().ContainSingle(d => d.Code == DiagnosticCodes.PdfCannotMakeLink).Which.Message.Should().Contain(named);
        compilation.Succeeded.Should().BeFalse();
    }

    [Fact]
    public void A_link_in_a_header_is_refused_at_compile()
    {
        var header = $"<w:hdr {W}><w:p><w:hyperlink r:id=\"rIdX0\"><w:r><w:t>example.org</w:t></w:r></w:hyperlink></w:p></w:hdr>";
        var docx = new TestDocx().Relationship(Hyperlink, "https://www.example.org", external: true).RawHeader(header).Paragraph("Dear Ms Example").Build();

        DocumentTemplate.Compile(docx, NoFields, [new HeliographPdfConverter()]).Diagnostics
            .Should().Contain(d => d.Code == DiagnosticCodes.PdfCannotMakeLink && d.Message.Contains("header", StringComparison.Ordinal));
    }

    private static MemoryStream Docx(string address)
    {
        var text = "Please see our <link> for what membership includes and how to renew it each year with the Federation.";
        var link = "<w:hyperlink r:id=\"rIdX0\"><w:r><w:rPr><w:color w:val=\"0563C1\"/><w:u w:val=\"single\"/></w:rPr><w:t>membership page</w:t></w:r></w:hyperlink>";
        var parts = text.Split("<link>");
        var paragraph = $"<w:p {W}><w:r><w:t xml:space=\"preserve\">{parts[0]}</w:t></w:r>{link}<w:r><w:t xml:space=\"preserve\">{parts[1]}</w:t></w:r></w:p>";
        var styles = "<w:styles xmlns:w=\"http://schemas.openxmlformats.org/wordprocessingml/2006/main\"><w:docDefaults><w:rPrDefault><w:rPr>"
            + "<w:rFonts w:ascii=\"Arial\" w:hAnsi=\"Arial\"/><w:sz w:val=\"22\"/></w:rPr></w:rPrDefault></w:docDefaults></w:styles>";
        return new TestDocx().Relationship(Hyperlink, address, external: true)
            .Part("word/styles.xml", styles, "application/vnd.openxmlformats-officedocument.wordprocessingml.styles+xml")
            .Relationship("http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles", "styles.xml", external: false)
            .RawParagraph(paragraph).Build();
    }

    private static byte[] Rendered(MemoryStream template)
    {
        var compilation = DocumentTemplate.Compile(template, NoFields, [new HeliographPdfConverter()]);
        compilation.Diagnostics.Should().BeEmpty();
        return compilation.Document!.Render(JsonDocument.Parse("{}").RootElement, Benchmark.British).Docx!;
    }

    private static Task<byte[]> Pdf(MemoryStream template) => new HeliographPdfConverter().ConvertAsync(Rendered(template));
}
