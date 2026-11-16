using Heliograph.Documents;
using Heliograph.Tests.Support;

namespace Heliograph.Tests;

/// <summary>
/// A condition must not cut through something that has a start and an end of its own, and a render must never
/// leave a file Word cannot open. See docs/edge-cases.md, camp 2.
/// </summary>
public class StructureTests
{
    private const string Word = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";
    private const string Relationships = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
    private const string FieldStart = "<w:r><w:fldChar w:fldCharType=\"begin\"/></w:r><w:r><w:instrText> PAGE </w:instrText></w:r><w:r><w:fldChar w:fldCharType=\"separate\"/></w:r>";
    private const string FieldEnd = "<w:r><w:fldChar w:fldCharType=\"end\"/></w:r>";
    private const string SectionBreak = "<w:pPr><w:sectPr><w:pgSz w:w=\"16838\" w:h=\"11906\" w:orient=\"landscape\"/></w:sectPr></w:pPr>";

    private static readonly FieldDictionary Dictionary = new(
    [
        new Field("Owes money", "owesMoney", FieldType.YesNo),
        new Field("Notes", "notes", FieldType.Text),
    ]);

    [Fact]
    public void A_condition_that_ends_in_the_middle_of_a_field_is_refused()
    {
        var docx = TestDocx.With("((if Owes money))").RawParagraph($"<w:p><w:r><w:t>Page </w:t></w:r>{FieldStart}<w:r><w:t>1</w:t></w:r></w:p>")
            .Paragraph("((end if))").RawParagraph($"<w:p>{FieldEnd}</w:p>");

        var diagnostic = Refusal(docx);

        diagnostic.Code.Should().Be(DiagnosticCodes.ConditionCutsThroughField);
        diagnostic.Location!.Paragraph.Should().Be(1);
    }

    [Fact]
    public void A_condition_inside_a_sentence_that_starts_in_the_middle_of_a_field_is_refused()
    {
        var docx = new TestDocx().RawParagraph(
            $"<w:p><w:r><w:t>Page </w:t></w:r>{FieldStart}<w:r><w:t>1((if Owes money))</w:t></w:r>{FieldEnd}<w:r><w:t> of many((end if)).</w:t></w:r></w:p>");

        Refusal(docx).Code.Should().Be(DiagnosticCodes.ConditionCutsThroughField);
    }

    [Theory]
    [InlineData(true, "Before.|Page 1 of the letter.|After.")]
    [InlineData(false, "Before.|After.")]
    public void A_whole_field_inside_a_condition_is_shown_or_hidden_with_it(bool owes, string expected)
    {
        var docx = TestDocx.With("Before.", "((if Owes money))")
            .RawParagraph($"<w:p><w:r><w:t xml:space=\"preserve\">Page </w:t></w:r>{FieldStart}<w:r><w:t>1</w:t></w:r>{FieldEnd}<w:r><w:t xml:space=\"preserve\"> of the letter.</w:t></w:r></w:p>")
            .Paragraph("((end if))").Paragraph("After.");

        var rendered = Render(docx, owes);

        string.Join('|', TestDocx.Paragraphs(rendered)).Should().Be(expected);
        WordRules.Problems(rendered).Should().BeEmpty();
    }

    [Fact]
    public void A_condition_that_holds_a_section_break_is_refused()
    {
        var docx = TestDocx.With("((if Owes money))").RawParagraph($"<w:p>{SectionBreak}<w:r><w:t>Last line of the section.</w:t></w:r></w:p>").Paragraph("((end if))");

        var diagnostic = Refusal(docx);

        diagnostic.Code.Should().Be(DiagnosticCodes.ConditionHoldsSectionBreak);
        diagnostic.Fix.Should().Contain("section break");
    }

    [Theory]
    [InlineData("((if Owes money))", "((end if))", 1)]
    [InlineData("((end if))", "((if Owes money))", 3)]
    public void A_condition_tag_on_the_line_that_carries_a_section_break_is_refused(string breakLineTag, string otherTag, int breakLine)
    {
        var tagWithBreak = $"<w:p>{SectionBreak}<w:r><w:t>{breakLineTag}</w:t></w:r></w:p>";
        var docx = breakLine == 1
            ? new TestDocx().RawParagraph(tagWithBreak).Paragraph("You owe us.").Paragraph(otherTag)
            : new TestDocx().Paragraph(otherTag).Paragraph("You owe us.").RawParagraph(tagWithBreak);

        var diagnostic = Refusal(docx);

        diagnostic.Code.Should().Be(DiagnosticCodes.ConditionHoldsSectionBreak);
        diagnostic.Location!.Paragraph.Should().Be(breakLine);
    }

    [Fact]
    public void Text_with_a_blank_line_does_not_split_the_paragraph_when_its_tag_is_inside_a_field()
    {
        var docx = new TestDocx().RawParagraph($"<w:p>{FieldStart}<w:r><w:t>((Notes))</w:t></w:r>{FieldEnd}<w:r><w:t xml:space=\"preserve\"> after</w:t></w:r></w:p>");

        var rendered = Render(docx, owes: true);

        TestDocx.Paragraphs(rendered).Should().Equal("One.\n\nTwo. after");
        WordRules.Problems(rendered).Should().BeEmpty();
    }

    [Fact]
    public void A_header_whose_whole_content_is_hidden_is_still_a_header()
    {
        var docx = TestDocx.With("Hello.").RawHeader(Paragraphs("((if Owes money))", "You owe us.", "((end if))"));

        var rendered = Render(docx, owes: false);

        TestDocx.HeaderAndFooterParagraphs(rendered).Should().Equal("");
        WordRules.Problems(rendered).Should().BeEmpty();
    }

    [Fact]
    public void A_footnote_whose_whole_content_is_hidden_is_still_a_footnote()
    {
        var footnotes = $"<w:footnotes xmlns:w=\"{Word}\"><w:footnote w:id=\"1\">{Paragraphs("((if Owes money))", "You owe us.", "((end if))")}</w:footnote></w:footnotes>";
        var docx = TestDocx.With("Hello.").Part("word/footnotes.xml", footnotes)
            .Relationship($"{Relationships}/footnotes", "footnotes.xml", external: false);

        var rendered = Render(docx, owes: false);

        TestDocx.PartParagraphs(rendered, "word/footnotes.xml").Should().Equal("");
        WordRules.Problems(rendered).Should().BeEmpty();
    }

    [Fact]
    public void A_text_box_whose_whole_content_is_hidden_is_still_a_text_box()
    {
        var docx = TestDocx.With("Hello.").RawParagraph(
            $"<w:p><w:r><w:pict><v:shape xmlns:v=\"urn:schemas-microsoft-com:vml\"><v:textbox><w:txbxContent>{Paragraphs("((if Owes money))", "You owe us.", "((end if))")}</w:txbxContent></v:textbox></v:shape></w:pict></w:r></w:p>");

        var rendered = Render(docx, owes: false);

        WordRules.Problems(rendered).Should().BeEmpty();
        string.Concat(TestDocx.Paragraphs(rendered)).Should().Be("Hello.");
    }

    [Theory]
    [InlineData("<w:bookmarkStart w:id=\"7\" w:name=\"amount\"/>", "", "<w:bookmarkEnd w:id=\"7\"/>")]
    [InlineData("", "<w:bookmarkStart w:id=\"7\" w:name=\"amount\"/>", "<w:bookmarkEnd w:id=\"7\"/>")]
    [InlineData("<w:bookmarkStart w:id=\"7\" w:name=\"amount\"/>", "<w:bookmarkEnd w:id=\"7\"/>", "")]
    public void Half_a_bookmark_is_not_left_behind_when_the_other_half_is_hidden(string before, string inside, string after)
    {
        var docx = new TestDocx().RawParagraph(
            $"<w:p>{before}<w:r><w:t>Before.</w:t></w:r></w:p>{Paragraphs("((if Owes money))")}<w:p>{inside}<w:r><w:t>You owe us.</w:t></w:r></w:p>"
            + $"{Paragraphs("((end if))")}<w:p><w:r><w:t>After.</w:t></w:r>{after}</w:p>");

        foreach (var owes in new[] { true, false })
        {
            var rendered = Render(docx, owes);

            WordRules.Problems(rendered).Should().BeEmpty($"owes money is {owes}");
            TestDocx.Paragraphs(rendered).Should().Contain("Before.").And.Contain("After.");
        }
    }

    [Fact]
    public void A_bookmark_an_editor_left_on_a_tag_line_does_no_harm()
    {
        var docx = TestDocx.With("Before.").RawParagraph(
            "<w:p><w:bookmarkStart w:id=\"0\" w:name=\"_GoBack\"/><w:r><w:t>((if Owes money))</w:t></w:r><w:bookmarkEnd w:id=\"0\"/></w:p>")
            .Paragraph("You owe us.").Paragraph("((end if))");

        var rendered = Render(docx, owes: true);

        TestDocx.Paragraphs(rendered).Should().Equal("Before.", "You owe us.");
        WordRules.Problems(rendered).Should().BeEmpty();
    }

    [Fact]
    public void A_template_with_more_conditions_than_could_ever_be_tried_in_every_combination_still_compiles_promptly()
    {
        // Thirty conditions have a thousand million combinations.
        var fields = Enumerable.Range(1, 30).Select(n => new Field($"Flag {n}", $"flag{n}", FieldType.YesNo)).ToList();
        var docx = new TestDocx();
        foreach (var field in fields)
        {
            docx.Paragraph($"((if {field.Name}))").Paragraph($"About {field.Name}.").Paragraph("((end if))");
        }

        var template = docx.Build().ToArray();
        var compiling = Task.Run(() => DocumentTemplate.Compile(new MemoryStream(template), new FieldDictionary(fields)));

        compiling.Wait(TimeSpan.FromSeconds(20)).Should().BeTrue("the compiler must not try every combination of thirty conditions");
        compiling.Result.Diagnostics.Should().BeEmpty();
    }

    private static string Paragraphs(params string[] texts) =>
        string.Concat(texts.Select(text => $"<w:p><w:r><w:t>{text}</w:t></w:r></w:p>"));

    private static Diagnostic Refusal(TestDocx docx)
    {
        using var stream = docx.Build();
        var compilation = DocumentTemplate.Compile(stream, Dictionary);
        compilation.Succeeded.Should().BeFalse();
        return compilation.Diagnostics.Should().ContainSingle().Subject;
    }

    private static byte[] Render(TestDocx docx, bool owes)
    {
        using var stream = docx.Build();
        var compilation = DocumentTemplate.Compile(stream, Dictionary);
        compilation.Diagnostics.Should().BeEmpty();
        var data = owes ? """{ "owesMoney": true, "notes": "One.\n\nTwo." }""" : """{ "owesMoney": false, "notes": "One.\n\nTwo." }""";
        var render = compilation.Document!.Render(Benchmark.Data(data), Benchmark.British);
        render.Diagnostics.Should().BeEmpty();
        return render.Docx!;
    }
}
