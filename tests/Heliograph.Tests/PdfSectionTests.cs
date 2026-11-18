using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using Heliograph.Documents;
using Heliograph.Pdf;
using Heliograph.Pdf.LibreOffice;
using Heliograph.Tests.Support;

namespace Heliograph.Tests;

/// <summary>
/// Sections in Heliograph's own PDF converter: a letter with a landscape part and an appendix, each section with its own
/// page and margins, headers and footers carried on from the section before unless it sets its own, and page numbers
/// restarted in Roman numerals, laid out as LibreOffice lays them out.
/// </summary>
public partial class PdfSectionTests
{
    private const string W = "xmlns:w=\"http://schemas.openxmlformats.org/wordprocessingml/2006/main\"";
    private const string R = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
    private const string Words = "Thank you for your application to the National Juggling Federation, which we received on 3 March and will answer soon.";
    private static readonly FieldDictionary NoFields = new([]);

    [ComparedPdfFact]
    public async Task Sections_are_laid_out_as_LibreOffice_lays_them_out()
    {
        var docx = Rendered(Letter());
        var (theirs, ours) = (await new LibreOfficePdfConverter().ConvertAsync(docx), await new HeliographPdfConverter().ConvertAsync(docx));

        PdfComparison.FirstDifference(PdfLines.Read(theirs), PdfLines.Read(ours), "LibreOffice").Difference.Should().BeNull();
        PageSizes(ours).Should().Equal(PageSizes(theirs));
    }

    [PdfLinesFact]
    public async Task Each_section_has_its_page_its_headers_and_its_page_numbers()
    {
        var pdf = await new HeliographPdfConverter().ConvertAsync(Rendered(Letter()));
        var lines = PdfLines.Read(pdf);
        string Header(int page) => lines.First(l => l.Page == page).Text;
        string Footer(int page) => lines.Last(l => l.Page == page).Text;

        PageSizes(pdf).Should().Equal("595x842", "842x595", "842x595", "595x842");
        (Header(1), Header(2), Header(4)).Should().Be(("Membership renewal", "Membership renewal", "Appendix: the fees"));
        Enumerable.Range(1, 4).Select(Footer).Should().Equal("Page 1", "Page i", "Page ii", "Page 3");
    }

    [VeraPdfTheory]
    [InlineData(true)]
    public async Task A_letter_in_sections_is_still_an_accessible_PDF(bool _) =>
        PdfValidator.FailuresAsPdfUa(await new HeliographPdfConverter().ConvertAsync(Rendered(Letter()))).Should().BeEmpty();

    [Theory]
    [InlineData("<w:type w:val=\"continuous\"/>", "does not start a new page")]
    [InlineData("<w:type w:val=\"oddPage\"/>", "odd or even page")]
    [InlineData("<w:pgBorders><w:top w:val=\"single\" w:sz=\"4\" w:space=\"24\" w:color=\"auto\"/></w:pgBorders>", "border round the page")]
    [InlineData("<w:lnNumType w:countBy=\"1\"/>", "line numbers")]
    [InlineData("<w:vAlign w:val=\"center\"/>", "middle")]
    [InlineData("<w:pgNumType w:fmt=\"chicago\"/>", "format of their own")]
    public void A_page_setting_it_cannot_lay_out_is_refused_at_compile(string setting, string named)
    {
        var docx = new TestDocx().RawParagraph($"<w:p {W}><w:pPr><w:sectPr>{setting}</w:sectPr></w:pPr><w:r><w:t>First part</w:t></w:r></w:p>").Paragraph("Second part").Build();

        DocumentTemplate.Compile(docx, NoFields, [new HeliographPdfConverter()]).Diagnostics
            .Should().ContainSingle(d => d.Code == DiagnosticCodes.PdfCannotLayOutSections).Which.Message.Should().Contain(named);
    }

    // A portrait part with a header and a numbered footer; a landscape part that carries them on and numbers its pages
    // again in Roman numerals; an appendix with a header of its own whose numbers go on in figures.
    private static MemoryStream Letter()
    {
        var styles = $"<w:styles {W}><w:docDefaults><w:rPrDefault><w:rPr><w:rFonts w:ascii=\"Arial\" w:hAnsi=\"Arial\"/><w:sz w:val=\"22\"/></w:rPr></w:rPrDefault>"
            + "<w:pPrDefault><w:pPr><w:spacing w:after=\"160\" w:line=\"259\" w:lineRule=\"auto\"/></w:pPr></w:pPrDefault></w:docDefaults></w:styles>";
        var docx = new TestDocx()
            .Part("word/styles.xml", styles, "application/vnd.openxmlformats-officedocument.wordprocessingml.styles+xml").Relationship($"{R}/styles", "styles.xml", external: false)
            .Part("word/header1.xml", Story("hdr", "<w:r><w:t>Membership renewal</w:t></w:r>"), "application/vnd.openxmlformats-officedocument.wordprocessingml.header+xml").Relationship($"{R}/header", "header1.xml", external: false)
            .Part("word/header2.xml", Story("hdr", "<w:r><w:t>Appendix: the fees</w:t></w:r>"), "application/vnd.openxmlformats-officedocument.wordprocessingml.header+xml").Relationship($"{R}/header", "header2.xml", external: false)
            .Part("word/footer1.xml", Story("ftr", "<w:r><w:t xml:space=\"preserve\">Page </w:t></w:r><w:fldSimple w:instr=\" PAGE \"><w:r><w:t>1</w:t></w:r></w:fldSimple>"), "application/vnd.openxmlformats-officedocument.wordprocessingml.footer+xml")
            .Relationship($"{R}/footer", "footer1.xml", external: false);
        const string Portrait = "<w:pgSz w:w=\"11906\" w:h=\"16838\"/><w:pgMar w:top=\"1440\" w:right=\"1440\" w:bottom=\"1440\" w:left=\"1440\" w:header=\"708\" w:footer=\"708\" w:gutter=\"0\"/>";
        const string Landscape = "<w:pgSz w:w=\"16838\" w:h=\"11906\" w:orient=\"landscape\"/><w:pgMar w:top=\"1080\" w:right=\"1080\" w:bottom=\"1080\" w:left=\"1080\" w:header=\"567\" w:footer=\"567\" w:gutter=\"0\"/>";
        Part(docx, 3, $"<w:headerReference w:type=\"default\" r:id=\"rIdX1\"/><w:footerReference w:type=\"default\" r:id=\"rIdX3\"/>{Portrait}");
        Part(docx, 30, $"{Landscape}<w:pgNumType w:fmt=\"lowerRoman\" w:start=\"1\"/>");
        for (var i = 0; i < 3; i++)
        {
            docx.RawParagraph($"<w:p {W}><w:r><w:t>{Words}</w:t></w:r></w:p>");
        }

        docx.RawParagraph($"<w:sectPr {W} xmlns:r=\"{R}\"><w:headerReference w:type=\"default\" r:id=\"rIdX2\"/>{Portrait}</w:sectPr>");
        return docx.Build();
    }

    // Paragraphs, the last ending a section with these settings.
    private static void Part(TestDocx docx, int paragraphs, string settings)
    {
        for (var i = 1; i < paragraphs; i++)
        {
            docx.RawParagraph($"<w:p {W}><w:r><w:t>{Words}</w:t></w:r></w:p>");
        }

        docx.RawParagraph($"<w:p {W} xmlns:r=\"{R}\"><w:pPr><w:sectPr>{settings}</w:sectPr></w:pPr><w:r><w:t>{Words}</w:t></w:r></w:p>");
    }

    private static string Story(string root, string runs) => $"<w:{root} {W}><w:p>{runs}</w:p></w:{root}>";

    // Each page's size in points, as Poppler reads it.
    private static List<string> PageSizes(byte[] pdf)
    {
        var path = Path.Combine(Directory.CreateTempSubdirectory("heliograph-sizes-").FullName, "letter.pdf");
        File.WriteAllBytes(path, pdf);
        var start = new ProcessStartInfo(PdfLines.Executable!.Replace("pdftotext", "pdfinfo", StringComparison.Ordinal)) { RedirectStandardOutput = true, UseShellExecute = false };
        foreach (var argument in new[] { "-f", "1", "-l", "99", path })
        {
            start.ArgumentList.Add(argument);
        }

        using var process = Process.Start(start)!;
        var output = process.StandardOutput.ReadToEnd();
        process.WaitForExit();
        Directory.Delete(Path.GetDirectoryName(path)!, recursive: true);
        return Size().Matches(output).Select(m => string.Create(CultureInfo.InvariantCulture, $"{Math.Round(double.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture))}x{Math.Round(double.Parse(m.Groups[2].Value, CultureInfo.InvariantCulture))}")).ToList();
    }

    [GeneratedRegex(@"Page\s+\d+ size:\s+([\d.]+) x ([\d.]+)")]
    private static partial Regex Size();

    private static byte[] Rendered(MemoryStream template)
    {
        var compilation = DocumentTemplate.Compile(template, NoFields, [new HeliographPdfConverter()]);
        compilation.Diagnostics.Should().BeEmpty();
        return compilation.Document!.Render(JsonDocument.Parse("{}").RootElement, Benchmark.British).Docx!;
    }
}
