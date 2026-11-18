using System.Text.Json;
using Heliograph.Documents;
using Heliograph.Pdf;
using Heliograph.Pdf.LibreOffice;
using Heliograph.Tests.Support;

namespace Heliograph.Tests;

/// <summary>
/// What a run holds beyond text in Heliograph's own PDF converter, which it used to leave out without a word: a position
/// tab (as Word's own headers and footers use) lining text up on the right or in the middle, a non-breaking hyphen that
/// keeps a telephone number on one line, an old-style page number; and what it still refuses.
/// </summary>
public class PdfRunContentTests
{
    private const string W = "xmlns:w=\"http://schemas.openxmlformats.org/wordprocessingml/2006/main\"";
    private static readonly FieldDictionary NoFields = new([]);

    public static TheoryData<string, string> Runs => new()
    {
        { "a telephone number with non-breaking hyphens at the end of a line",
            "<w:r><w:t xml:space=\"preserve\">Please telephone the membership team at the National Juggling Federation on 0300</w:t></w:r><w:r><w:noBreakHyphen/><w:t>123</w:t></w:r><w:r><w:noBreakHyphen/><w:t>4567 today.</w:t></w:r>" },
        { "a page number in the body", "<w:r><w:t xml:space=\"preserve\">This is page </w:t></w:r><w:fldSimple w:instr=\" PAGE \"><w:r><w:t>9</w:t></w:r></w:fldSimple><w:r><w:t>.</w:t></w:r>" },
    };

    [ComparedPdfTheory]
    [MemberData(nameof(Runs))]
    public async Task Run_content_is_laid_out_as_LibreOffice_lays_it_out(string kind, string runs)
    {
        var docx = Rendered(Letter(runs));
        var expected = PdfLines.Read(await new LibreOfficePdfConverter().ConvertAsync(docx));
        var actual = PdfLines.Read(await new HeliographPdfConverter().ConvertAsync(docx));

        PdfComparison.FirstDifference(expected, actual, "LibreOffice").Difference.Should().BeNull(kind);
    }

    // LibreOffice takes a position tab for an ordinary one, and leaves an old-style page number out: the format, and Word
    // (WordProbes, probe-run-content), are the judges of these.
    [PdfLinesFact]
    public async Task A_position_tab_lines_text_up_on_the_right_or_in_the_middle_of_the_margins()
    {
        var right = PdfLines.Read(await new HeliographPdfConverter().ConvertAsync(Rendered(Letter("<w:r><w:ptab w:relativeTo=\"margin\" w:alignment=\"right\" w:leader=\"none\"/><w:t>4567</w:t></w:r>"))));
        var middle = PdfLines.Read(await new HeliographPdfConverter().ConvertAsync(Rendered(Letter("<w:r><w:ptab w:relativeTo=\"margin\" w:alignment=\"center\" w:leader=\"none\"/><w:t>4567</w:t></w:r>"))));

        // Four figures of Arial at 11 points are 24.5 points wide, between margins 72 points in on Letter paper.
        right.Single(l => l.Text == "4567").Left.Should().BeApproximately(540 - 24.5, 1);
        middle.Single(l => l.Text == "4567").Left.Should().BeApproximately(306 - 12.2, 1);
    }

    [PdfLinesFact]
    public async Task An_old_style_page_number_is_the_page_number()
    {
        var lines = PdfLines.Read(await new HeliographPdfConverter().ConvertAsync(Rendered(Letter("<w:r><w:t xml:space=\"preserve\">This is page </w:t></w:r><w:r><w:pgNum/></w:r><w:r><w:t>.</w:t></w:r>"))));

        lines.Should().Contain(l => l.Text == "This is page 1.");
    }

    [Theory]
    [InlineData("<w:r><w:sym w:font=\"Wingdings\" w:char=\"F0FC\"/></w:r>", "symbol font")]
    [InlineData("<w:r><w:t>member</w:t></w:r><w:r><w:softHyphen/></w:r><w:r><w:t>ship</w:t></w:r>", "optional hyphen")]
    public void Run_content_it_cannot_lay_out_is_refused_at_compile(string runs, string named)
    {
        var docx = new TestDocx().RawParagraph($"<w:p {W}>{runs}</w:p>").Build();

        DocumentTemplate.Compile(docx, NoFields, [new HeliographPdfConverter()]).Diagnostics
            .Should().ContainSingle(d => d.Code == DiagnosticCodes.PdfCannotDrawFormatting).Which.Message.Should().Contain(named);
    }

    [Theory]
    [InlineData(" NUMPAGES ", "in the body")]
    public void A_count_of_pages_in_the_body_is_refused_at_compile(string instruction, string named)
    {
        var docx = new TestDocx().RawParagraph($"<w:p {W}><w:fldSimple w:instr=\"{instruction}\"><w:r><w:t>9</w:t></w:r></w:fldSimple></w:p>").Build();

        DocumentTemplate.Compile(docx, NoFields, [new HeliographPdfConverter()]).Diagnostics
            .Should().ContainSingle(d => d.Code == DiagnosticCodes.PdfCannotWorkOutField).Which.Message.Should().Contain(named);
    }

    private static MemoryStream Letter(string runs)
    {
        var styles = $"<w:styles {W}><w:docDefaults><w:rPrDefault><w:rPr><w:rFonts w:ascii=\"Arial\" w:hAnsi=\"Arial\"/><w:sz w:val=\"22\"/></w:rPr></w:rPrDefault>"
            + "<w:pPrDefault><w:pPr><w:spacing w:after=\"160\" w:line=\"259\" w:lineRule=\"auto\"/></w:pPr></w:pPrDefault></w:docDefaults></w:styles>";
        return new TestDocx().Part("word/styles.xml", styles, "application/vnd.openxmlformats-officedocument.wordprocessingml.styles+xml")
            .Relationship("http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles", "styles.xml", external: false)
            .RawParagraph($"<w:p {W}><w:r><w:t>Dear Ms Example,</w:t></w:r></w:p>")
            .RawParagraph($"<w:p {W}>{runs}</w:p>")
            .RawParagraph($"<w:p {W}><w:r><w:t>Yours sincerely,</w:t></w:r></w:p>").Build();
    }

    private static byte[] Rendered(MemoryStream template)
    {
        var compilation = DocumentTemplate.Compile(template, NoFields, [new HeliographPdfConverter()]);
        compilation.Diagnostics.Should().BeEmpty();
        return compilation.Document!.Render(JsonDocument.Parse("{}").RootElement, Benchmark.British).Docx!;
    }
}
