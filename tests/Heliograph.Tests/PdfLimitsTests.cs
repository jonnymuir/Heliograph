using Heliograph.Documents;
using Heliograph.Library;
using Heliograph.Pdf;
using Heliograph.Tests.Support;

namespace Heliograph.Tests;

/// <summary>
/// A host that makes its PDFs with Heliograph's own converter is told, when a template is compiled, what that converter
/// cannot lay out yet, so no PDF is ever quietly missing something.
/// </summary>
public class PdfLimitsTests
{
    private const string W = "xmlns:w=\"http://schemas.openxmlformats.org/wordprocessingml/2006/main\"";
    private const string Drawing = "xmlns:wp=\"http://schemas.openxmlformats.org/drawingml/2006/wordprocessingDrawing\" xmlns:a=\"http://schemas.openxmlformats.org/drawingml/2006/main\"";
    private static readonly IReadOnlyList<IDocumentCheck> Checks = [new HeliographPdfConverter()];
    private static readonly FieldDictionary Dictionary = new([new Field("Member name", "memberName", FieldType.Text)]);

    [Theory]
    [InlineData("acknowledgement", "document.docx")]
    [InlineData("acknowledgement", "document.word-365.docx")]
    [InlineData("decision-letter", "document.docx")]
    [InlineData("annual-statement", "document.docx")]
    public void Every_benchmark_letter_is_within_what_the_converter_lays_out(string letter, string file)
    {
        using var docx = File.OpenRead(Benchmark.PathTo(letter, file));

        DocumentTemplate.Compile(docx, Benchmark.Dictionary(letter), Checks).Diagnostics
            .Where(d => d.Code.StartsWith("HG4", StringComparison.Ordinal)).Should().BeEmpty();
    }

    public static TheoryData<string, string, string> Limits => new()
    {
        { DiagnosticCodes.PdfCannotDrawPicture, $"<w:p {W}><w:r><w:drawing><wp:inline {Drawing}><a:graphic><a:graphicData uri=\"http://schemas.openxmlformats.org/drawingml/2006/picture\"/></a:graphic></wp:inline></w:drawing></w:r></w:p>", "picture" },
        { DiagnosticCodes.PdfCannotDrawShape, $"<w:p {W}><w:r><w:drawing><wp:anchor {Drawing}><a:graphic><a:graphicData uri=\"http://schemas.microsoft.com/office/word/2010/wordprocessingShape\"/></a:graphic></wp:anchor></w:drawing></w:r></w:p>", "text box" },
        { DiagnosticCodes.PdfCannotLayOutNotes, $"<w:p {W}><w:r><w:t>See note</w:t></w:r><w:r><w:endnoteReference w:customMarkFollows=\"1\" w:id=\"1\"/><w:t>*</w:t></w:r></w:p>", "mark of its own" },
        { DiagnosticCodes.PdfCannotLayOutSections, $"<w:p {W}><w:pPr><w:sectPr><w:type w:val=\"continuous\"/><w:cols w:num=\"2\"/></w:sectPr></w:pPr><w:r><w:t>Before the break</w:t></w:r></w:p>", "does not start a new page" },
        { DiagnosticCodes.PdfCannotLayOutColumns, $"<w:p {W}><w:pPr><w:sectPr><w:cols w:num=\"2\" w:equalWidth=\"0\"><w:col w:w=\"3000\"/><w:col w:w=\"5000\"/></w:cols></w:sectPr></w:pPr></w:p>", "different widths" },
        { DiagnosticCodes.PdfCannotWorkOutField, $"<w:p {W}><w:fldSimple w:instr=\" DATE \\@ &quot;d MMMM yyyy&quot; \"><w:r><w:t>1 June</w:t></w:r></w:fldSimple></w:p>", "DATE field" },
        { DiagnosticCodes.PdfCannotMakeLink, $"<w:p {W}><w:hyperlink w:anchor=\"top\"><w:r><w:t>Go to the top</w:t></w:r></w:hyperlink></w:p>", "link" },
        { DiagnosticCodes.PdfCannotDrawFormatting, $"<w:p {W}><w:r><w:rPr><w:outline/></w:rPr><w:t>Outlined</w:t></w:r></w:p>", "outlined" },
        { DiagnosticCodes.PdfCannotLayOutDirection, $"<w:p {W}><w:pPr><w:bidi/></w:pPr><w:r><w:t>Right to left</w:t></w:r></w:p>", "right-to-left" },
        { DiagnosticCodes.PdfCannotLayOutTable, $"<w:tbl {W}><w:tblGrid><w:gridCol w:w=\"4000\"/></w:tblGrid><w:tr><w:tc><w:tbl><w:tblGrid><w:gridCol w:w=\"2000\"/></w:tblGrid><w:tr><w:tc><w:tcPr><w:vMerge w:val=\"restart\"/></w:tcPr><w:p><w:r><w:t>Inner</w:t></w:r></w:p></w:tc></w:tr><w:tr><w:tc><w:tcPr><w:vMerge/></w:tcPr><w:p/></w:tc></w:tr></w:tbl><w:p/></w:tc></w:tr></w:tbl>", "merged down a column" },
        { DiagnosticCodes.PdfCannotAlignTab, $"<w:p {W}><w:pPr><w:tabs><w:tab w:val=\"bar\" w:pos=\"4320\"/></w:tabs></w:pPr><w:r><w:t>Name</w:t></w:r></w:p>", "bar" },
        { DiagnosticCodes.PdfCannotAlignTab, $"<w:p {W}><w:pPr><w:jc w:val=\"center\"/></w:pPr><w:r><w:t>Name</w:t><w:tab/><w:t>Date</w:t></w:r></w:p>", "centred or right-aligned paragraph" },
    };

    [Theory]
    [MemberData(nameof(Limits))]
    public void Something_the_converter_cannot_lay_out_stops_the_template_with_its_place_and_a_fix(string code, string xml, string named)
    {
        var compilation = DocumentTemplate.Compile(TestDocx.With("Dear ((Member name)),").RawParagraph(xml).Build(), Dictionary, Checks);

        compilation.Succeeded.Should().BeFalse();
        var problem = compilation.Diagnostics.Should().ContainSingle(d => d.Code == code).Subject;
        problem.Message.Should().Contain(named);
        problem.Fix.Should().Contain("LibreOffice");
        problem.Location.Should().NotBeNull();
        problem.Location!.Part.Should().Be("document");
        problem.Location.Paragraph.Should().BeGreaterThan(1, "the first paragraph is the greeting");
    }

    [Fact]
    public void A_font_without_a_look_alike_is_a_warning_and_the_template_still_compiles()
    {
        var garamond = $"<w:p {W}><w:r><w:rPr><w:rFonts w:ascii=\"Garamond\" w:hAnsi=\"Garamond\"/></w:rPr><w:t>In Garamond</w:t></w:r></w:p>";

        var compilation = DocumentTemplate.Compile(TestDocx.With("Dear ((Member name)),").RawParagraph(garamond).Build(), Dictionary, Checks);

        compilation.Succeeded.Should().BeTrue();
        var warning = compilation.Diagnostics.Should().ContainSingle(d => d.Code == DiagnosticCodes.PdfFontDrawnInLookAlike).Subject;
        (warning.Severity, warning.Message.Contains("Garamond", StringComparison.Ordinal)).Should().Be((DiagnosticSeverity.Warning, true));
    }

    [Fact]
    public void Each_limit_is_reported_once_in_a_part_however_often_it_occurs()
    {
        var outlined = $"<w:p {W}><w:r><w:rPr><w:outline/></w:rPr><w:t>Outlined</w:t></w:r></w:p>";

        var compilation = DocumentTemplate.Compile(TestDocx.With("Dear ((Member name)),").RawParagraph(outlined).RawParagraph(outlined).Build(), Dictionary, Checks);

        compilation.Diagnostics.Where(d => d.Code == DiagnosticCodes.PdfCannotDrawFormatting).Should().ContainSingle();
    }

    [Fact]
    public void A_host_that_makes_PDFs_another_way_is_not_held_to_these_limits()
    {
        var outlined = $"<w:p {W}><w:r><w:rPr><w:outline/></w:rPr><w:t>Outlined</w:t></w:r></w:p>";

        DocumentTemplate.Compile(TestDocx.With("Dear ((Member name)),").RawParagraph(outlined).Build(), Dictionary).Succeeded.Should().BeTrue();
    }

    [Fact]
    public async Task A_draft_is_held_to_the_limits_and_a_published_version_still_renders_after_the_host_changes_route()
    {
        var store = new InMemoryCorrespondenceStore();
        var dictionaries = new InMemoryDictionarySource();
        dictionaries["members"] = """{ "fields": [ { "name": "Member name", "key": "memberName", "type": "text" } ] }""";
        var outlined = $"<w:p {W}><w:r><w:rPr><w:outline/></w:rPr><w:t>Outlined</w:t></w:r></w:p>";
        var content = new CorrespondenceContent { Name = "Welcome", Dictionary = "members", Document = TestDocx.With("Dear ((Member name)),").RawParagraph(outlined).Build().ToArray() };

        var before = new CorrespondenceLibrary(store, dictionaries, TimeProvider.System);
        await before.SaveDraftAsync("welcome", content, 0);
        (await before.PublishAsync("welcome")).Published.Should().BeTrue("without the built-in route nothing limits it");

        var after = new CorrespondenceLibrary(store, dictionaries, TimeProvider.System, documentChecks: Checks);
        (await after.CheckAsync("welcome")).Diagnostics.Should().Contain(d => d.Code == DiagnosticCodes.PdfCannotDrawFormatting);
        (await after.PublishAsync("welcome")).Published.Should().BeFalse();
        (await after.RenderAsync("welcome", Benchmark.Data("""{ "memberName": "Alex" }"""), Benchmark.British)).Rendered.Should().NotBeNull(
            "a version already published renders as it was published");
    }
}
