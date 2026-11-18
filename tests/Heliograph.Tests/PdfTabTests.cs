using System.Text.Json;
using System.Text.RegularExpressions;
using Heliograph.Documents;
using Heliograph.Pdf;
using Heliograph.Pdf.LibreOffice;
using Heliograph.Tests.Support;

namespace Heliograph.Tests;

/// <summary>
/// Tab stops in Heliograph's own PDF converter: text after a right, centred or decimal stop lined up on it, and leaders
/// of dots, hyphens or a line before it, laid out as LibreOffice lays them out.
/// </summary>
public class PdfTabTests
{
    private const string W = "xmlns:w=\"http://schemas.openxmlformats.org/wordprocessingml/2006/main\"";
    private static readonly FieldDictionary NoFields = new([]);

    public static TheoryData<string, string> Stops => new()
    {
        { "right", "<w:tab w:val=\"right\" w:pos=\"9026\"/>" },
        { "centred", "<w:tab w:val=\"center\" w:pos=\"4513\"/>" },
        { "decimal", "<w:tab w:val=\"decimal\" w:pos=\"7200\"/>" },
        { "right with dots", "<w:tab w:val=\"right\" w:leader=\"dot\" w:pos=\"9026\"/>" },
        { "left with a line", "<w:tab w:val=\"left\" w:leader=\"underscore\" w:pos=\"5040\"/>" },
        { "decimal with hyphens", "<w:tab w:val=\"decimal\" w:leader=\"hyphen\" w:pos=\"7200\"/>" },
        { "two stops", "<w:tab w:val=\"center\" w:pos=\"4513\"/><w:tab w:val=\"right\" w:pos=\"9026\"/>" },
    };

    [ComparedPdfTheory]
    [MemberData(nameof(Stops))]
    public async Task Text_after_a_tab_lines_up_on_its_stop_as_LibreOffice_lines_it_up(string kind, string tabs)
    {
        var docx = Rendered(Letter(tabs));
        var expected = PdfLines.Read(await new LibreOfficePdfConverter().ConvertAsync(docx));
        var actual = PdfLines.Read(await new HeliographPdfConverter().ConvertAsync(docx));

        actual.Select(l => Words(l.Text)).Should().Equal(expected.Select(l => Words(l.Text)), kind);
        actual.Zip(expected).Should().OnlyContain(pair => Math.Abs(pair.First.Top - pair.Second.Top) <= 1.5, kind);
        actual.Zip(expected).Should().OnlyContain(pair => Math.Abs(pair.First.Left - pair.Second.Left) <= 1.5, kind);
    }

    [VeraPdfTheory]
    [InlineData("<w:tab w:val=\"right\" w:leader=\"dot\" w:pos=\"9026\"/>")]
    public async Task A_leader_is_passed_over_and_the_PDF_is_still_accessible(string tabs) =>
        PdfValidator.FailuresAsPdfUa(await new HeliographPdfConverter().ConvertAsync(Rendered(Letter(tabs)))).Should().BeEmpty();

    // The words of a line without its leader, which each converter draws with as many dots as fit.
    private static string Words(string line) =>
        string.Join(' ', Regex.Replace(line, "[._\\-·]{3,}", " ").Split(' ', StringSplitOptions.RemoveEmptyEntries));

    // Lines a letter might line up: a reference on the right, a total on its decimal point.
    private static MemoryStream Letter(string tabs)
    {
        string[] lines = ["Our reference\tNJF-2027-0042", "Membership fee\t£45.00", "Late payment\t£3.50", "Total\t£1,048.50", "Dear Ms Example\tFirst\tSecond"];
        var styles = $"<w:styles {W}><w:docDefaults><w:rPrDefault><w:rPr><w:rFonts w:ascii=\"Arial\" w:hAnsi=\"Arial\"/><w:sz w:val=\"22\"/></w:rPr></w:rPrDefault>"
            + "<w:pPrDefault><w:pPr><w:spacing w:after=\"120\"/></w:pPr></w:pPrDefault></w:docDefaults></w:styles>";
        var docx = new TestDocx().Part("word/styles.xml", styles, "application/vnd.openxmlformats-officedocument.wordprocessingml.styles+xml")
            .Relationship("http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles", "styles.xml", external: false);
        foreach (var line in lines)
        {
            var runs = string.Join("<w:r><w:tab/></w:r>", line.Split('\t').Select(part => $"<w:r><w:t xml:space=\"preserve\">{part}</w:t></w:r>"));
            docx.RawParagraph($"<w:p {W}><w:pPr><w:tabs>{tabs}</w:tabs></w:pPr>{runs}</w:p>");
        }

        return docx.Build();
    }

    private static byte[] Rendered(MemoryStream template)
    {
        var compilation = DocumentTemplate.Compile(template, NoFields, [new HeliographPdfConverter()]);
        compilation.Diagnostics.Should().BeEmpty();
        return compilation.Document!.Render(JsonDocument.Parse("{}").RootElement, Benchmark.British).Docx!;
    }
}
