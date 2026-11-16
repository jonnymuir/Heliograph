using Heliograph.Documents;
using Heliograph.Tests.Support;

namespace Heliograph.Tests;

/// <summary>A condition inside a sentence: ((if Field)) and ((end if)) in the same paragraph, around part of its text.</summary>
public class ConditionInSentenceTests
{
    private const string Table = "<w:tbl><w:tr><w:tc><w:p><w:r><w:t>In the table</w:t></w:r></w:p></w:tc></w:tr></w:tbl>";

    private static readonly FieldDictionary Dictionary = new(
    [
        new Field("Applicant name", "applicantName", FieldType.Text),
        new Field("Owes money", "owesMoney", FieldType.YesNo),
        new Field("Is a member", "isAMember", FieldType.YesNo),
        new Field("Amount owed", "amountOwed", FieldType.Money),
    ]);

    [Theory]
    [InlineData(true, "Pay now, because you owe us.")]
    [InlineData(false, "Pay now.")]
    public void A_condition_can_sit_inside_a_sentence(bool owes, string expected)
    {
        var docx = TestDocx.With("Pay now((if Owes money)), because you owe us((end if)).");

        Paragraphs(docx, Owes(owes)).Should().Equal(expected);
    }

    [Theory]
    [InlineData(true, "You owe £12.50. Bye.")]
    [InlineData(false, " Bye.")]
    public void A_condition_inside_a_sentence_can_hold_a_value(bool owes, string expected)
    {
        var docx = TestDocx.With("((if Owes money))You owe £((Amount owed)).((end if)) Bye.");
        var data = owes ? """{ "owesMoney": true, "amountOwed": 12.5 }""" : Owes(false);

        Paragraphs(docx, data).Should().Equal(expected);
    }

    [Theory]
    [InlineData(true, true, "A member who owes.")]
    [InlineData(true, false, "A member.")]
    [InlineData(false, true, "A.")]
    public void Conditions_inside_a_sentence_can_sit_inside_each_other(bool member, bool owes, string expected)
    {
        var docx = TestDocx.With("A((if Is a member)) member((if Owes money)) who owes((end if))((end if)).");
        var data = $$"""{ "isAMember": {{(member ? "true" : "false")}}, "owesMoney": {{(owes ? "true" : "false")}} }""";

        Paragraphs(docx, data).Should().Equal(expected);
    }

    [Fact]
    public void A_condition_inside_a_sentence_keeps_the_formatting_of_what_it_shows()
    {
        using var stream = new TestDocx().RawParagraph(
            "<w:p><w:r><w:t xml:space=\"preserve\">Pay ((if Owes money))now, </w:t></w:r>"
            + "<w:r><w:rPr><w:i/></w:rPr><w:t>today((end if)) please</w:t></w:r><w:r><w:t>.</w:t></w:r></w:p>").Build();
        var document = DocumentTemplate.Compile(stream, Dictionary).Document!;

        var shown = document.Render(Benchmark.Data(Owes(true)), Benchmark.British).Docx!;
        var hidden = document.Render(Benchmark.Data(Owes(false)), Benchmark.British).Docx!;

        TestDocx.Paragraphs(shown).Should().Equal("Pay now, today please.");
        TestDocx.ItalicText(shown).Should().Be("today please");
        TestDocx.Paragraphs(hidden).Should().Equal("Pay  please.");
        TestDocx.ItalicText(hidden).Should().Be(" please");
    }

    [Fact]
    public void A_paragraph_that_was_all_condition_leaves_nothing_behind_when_hidden()
    {
        var docx = TestDocx.With("Before.", "((if Owes money))You owe us.((end if))", "((if Owes money))A((end if))((if Is a member))B((end if))", "After.");

        Paragraphs(docx, """{ "owesMoney": false, "isAMember": false }""").Should().Equal("Before.", "After.");
        Paragraphs(docx, """{ "owesMoney": false, "isAMember": true }""").Should().Equal("Before.", "B", "After.");
    }

    [Theory]
    [InlineData("((if Owes money))A((end if))((if Is a member))((end if))")]
    [InlineData("((if Is a member))((end if))((if Owes money))A((end if))")]
    [InlineData("((if Is a member))((if Owes money))A((end if))((end if)) ")]
    public void A_paragraph_left_empty_goes_whichever_order_its_conditions_come_in(string paragraph)
    {
        var docx = TestDocx.With("Before.", paragraph, "After.");

        Paragraphs(docx, """{ "owesMoney": false, "isAMember": true }""").Should().Equal("Before.", "After.");
    }

    [Fact]
    public void A_table_cell_whose_whole_content_is_hidden_is_still_a_cell()
    {
        var docx = new TestDocx().RawParagraph(
            "<w:tbl><w:tr><w:tc><w:p><w:r><w:t>((if Owes money))</w:t></w:r></w:p><w:p><w:r><w:t>You owe us.</w:t></w:r></w:p>"
            + "<w:p><w:r><w:t>((end if))</w:t></w:r></w:p></w:tc>"
            + "<w:tc><w:p><w:r><w:t>((if Owes money))Pay.((end if))</w:t></w:r></w:p></w:tc></w:tr></w:tbl>");

        var render = Render(docx, Owes(false));

        TestDocx.CellParagraphCounts(render.Docx!).Should().Equal(1, 1);
        TestDocx.Paragraphs(render.Docx!).Should().Equal("", "");
    }

    [Fact]
    public void A_condition_opened_inside_a_sentence_and_not_closed_in_it_is_reported_once()
    {
        var diagnostics = Compile(TestDocx.With("Hello. ((if Owes money)) You owe us.", "More.", "((end if))")).Diagnostics;

        var diagnostic = diagnostics.Should().ContainSingle().Subject;
        diagnostic.Code.Should().Be(DiagnosticCodes.ConditionNotClosed);
        diagnostic.Location!.Paragraph.Should().Be(1);
        diagnostic.Fix.Should().Contain("same paragraph");
    }

    [Fact]
    public void An_end_inside_a_sentence_with_no_condition_open_in_it_is_reported_once()
    {
        var diagnostics = Compile(TestDocx.With("((if Owes money))", "You owe us.", "Thank you. ((end if))")).Diagnostics;

        var diagnostic = diagnostics.Should().ContainSingle().Subject;
        diagnostic.Code.Should().Be(DiagnosticCodes.EndWithoutCondition);
        diagnostic.Location!.Paragraph.Should().Be(3);
    }

    [Fact]
    public void An_end_before_its_condition_in_the_same_sentence_is_reported()
    {
        Compile(TestDocx.With("Oops ((end if)) and ((if Owes money)) here.")).Diagnostics.Select(d => d.Code)
            .Should().Equal(DiagnosticCodes.EndWithoutCondition, DiagnosticCodes.ConditionNotClosed);
    }

    private static string Owes(bool owes) => owes ? """{ "owesMoney": true }""" : """{ "owesMoney": false }""";

    private static DocumentCompilation Compile(TestDocx docx)
    {
        using var stream = docx.Build();
        return DocumentTemplate.Compile(stream, Dictionary);
    }

    private static DocumentRender Render(TestDocx docx, string data)
    {
        var compilation = Compile(docx);
        compilation.Diagnostics.Should().BeEmpty();
        return compilation.Document!.Render(Benchmark.Data(data), Benchmark.British);
    }

    private static IReadOnlyList<string> Paragraphs(TestDocx docx, string data)
    {
        var render = Render(docx, data);
        render.Diagnostics.Should().BeEmpty();
        return TestDocx.Paragraphs(render.Docx!);
    }
}
