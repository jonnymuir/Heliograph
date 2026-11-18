using System.Text.Json;
using Heliograph.Documents;
using Heliograph.Pdf;
using Heliograph.Pdf.LibreOffice;
using Heliograph.Tests.Support;

namespace Heliograph.Tests;

/// <summary>
/// Cells merged down a column in Heliograph's own PDF converter: drawn once down their rows, the last of which grows to fit
/// the merged cell's text, laid out as LibreOffice lays them out.
/// </summary>
public class PdfMergedCellTests
{
    private const string W = "xmlns:w=\"http://schemas.openxmlformats.org/wordprocessingml/2006/main\"";
    private static readonly FieldDictionary NoFields = new([]);

    public static TheoryData<string, string[]> Merges => new()
    {
        // Each row's cell in the first column: restart, continue or plain.
        { "two rows", ["restart", "continue", ""] },
        { "three rows", ["restart", "continue", "continue"] },
        { "a tall merged cell", ["restart", "continue", ""] },
    };

    [ComparedPdfTheory]
    [MemberData(nameof(Merges))]
    public async Task Cells_merged_down_a_column_are_laid_out_as_LibreOffice_lays_them_out(string kind, string[] merges)
    {
        var tall = kind == "a tall merged cell";
        var docx = Rendered(Merged(merges, tall));
        var expected = PdfLines.Read(await new LibreOfficePdfConverter().ConvertAsync(docx));
        var actual = PdfLines.Read(await new HeliographPdfConverter().ConvertAsync(docx));

        actual.Select(l => l.Text).Should().Equal(expected.Select(l => l.Text), kind);
        actual.Zip(expected).Should().OnlyContain(pair => Math.Abs(pair.First.Top - pair.Second.Top) <= 1.5, kind);
    }

    // A three-row Table Grid table whose first column merges down as asked; the merged cell's text is long when tall.
    private static MemoryStream Merged(string[] merges, bool tall)
    {
        var styles = $"<w:styles {W}><w:docDefaults><w:rPrDefault><w:rPr><w:rFonts w:ascii=\"Arial\" w:hAnsi=\"Arial\"/><w:sz w:val=\"22\"/></w:rPr></w:rPrDefault>"
            + $"</w:docDefaults><w:style w:type=\"paragraph\" w:default=\"1\" w:styleId=\"Normal\"><w:name w:val=\"Normal\"/></w:style>{PdfTableTests.TableNormal}{PdfTableTests.TableGrid}</w:styles>";
        var docx = new TestDocx().Part("word/styles.xml", styles, "application/vnd.openxmlformats-officedocument.wordprocessingml.styles+xml")
            .Relationship("http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles", "styles.xml", external: false);
        var first = tall ? "Membership for the whole year, with entry to every meeting, the newsletter and insurance at all events" : "Membership";
        var rows = merges.Select((merge, r) =>
        {
            var vMerge = merge switch { "restart" => "<w:vMerge w:val=\"restart\"/>", "continue" => "<w:vMerge/>", _ => "" };
            var text = merge == "continue" ? "" : r == 0 ? first : $"Row {r + 1}";
            return $"<w:tr><w:tc><w:tcPr><w:tcW w:w=\"2400\" w:type=\"dxa\"/>{vMerge}</w:tcPr><w:p><w:r><w:t>{text}</w:t></w:r></w:p></w:tc>"
                + $"<w:tc><w:tcPr><w:tcW w:w=\"3000\" w:type=\"dxa\"/></w:tcPr><w:p><w:r><w:t>Detail {r + 1}</w:t></w:r></w:p></w:tc></w:tr>";
        });
        docx.RawParagraph($"<w:p {W}><w:r><w:t>Your membership.</w:t></w:r></w:p>");
        docx.RawParagraph($"<w:tbl {W}><w:tblPr><w:tblStyle w:val=\"TableGrid\"/><w:tblW w:w=\"5400\" w:type=\"dxa\"/></w:tblPr><w:tblGrid><w:gridCol w:w=\"2400\"/><w:gridCol w:w=\"3000\"/></w:tblGrid>{string.Concat(rows)}</w:tbl>");
        docx.RawParagraph($"<w:p {W}><w:r><w:t>Thank you.</w:t></w:r></w:p>");
        return docx.Build();
    }

    private static byte[] Rendered(MemoryStream template)
    {
        var compilation = DocumentTemplate.Compile(template, NoFields, [new HeliographPdfConverter()]);
        compilation.Diagnostics.Should().BeEmpty();
        return compilation.Document!.Render(JsonDocument.Parse("{}").RootElement, Benchmark.British).Docx!;
    }
}
