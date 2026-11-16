using Heliograph.Documents;
using Heliograph.Tests.Support;

namespace Heliograph.Tests;

/// <summary>Showing content only when a yes or no field is yes: ((if Field)) to ((end if)).</summary>
public class ConditionTests
{
    private const string Table = "<w:tbl><w:tr><w:tc><w:p><w:r><w:t>In the table</w:t></w:r></w:p></w:tc></w:tr></w:tbl>";

    private static readonly FieldDictionary Dictionary = new(
    [
        new Field("Applicant name", "applicantName", FieldType.Text),
        new Field("Owes money", "owesMoney", FieldType.YesNo),
        new Field("Is a member", "isAMember", FieldType.YesNo),
        new Field("Amount owed", "amountOwed", FieldType.Money),
    ]);

    [Fact]
    public void Content_between_the_tags_is_shown_when_the_field_is_yes()
    {
        Paragraphs(TestDocx.With("Before.", "((if Owes money))", "You owe us.", "((end if))", "After."), """{ "owesMoney": true }""")
            .Should().Equal("Before.", "You owe us.", "After.");
    }

    [Fact]
    public void Content_between_the_tags_is_removed_when_the_field_is_no()
    {
        Paragraphs(TestDocx.With("Before.", "((if Owes money))", "You owe us.", "((end if))", "After."), """{ "owesMoney": false }""")
            .Should().Equal("Before.", "After.");
    }

    [Theory]
    [InlineData(true, true, "Outer.|Inner.|Outer again.")]
    [InlineData(true, false, "Outer.|Outer again.")]
    [InlineData(false, true, "")]
    [InlineData(false, false, "")]
    public void A_condition_inside_a_condition_needs_both(bool member, bool owes, string expected)
    {
        var docx = TestDocx.With("((if Is a member))", "Outer.", "((if Owes money))", "Inner.", "((end if))", "Outer again.", "((end if))");
        var data = $$"""{ "isAMember": {{(member ? "true" : "false")}}, "owesMoney": {{(owes ? "true" : "false")}} }""";

        string.Join('|', Paragraphs(docx, data)).Should().Be(expected);
    }

    [Fact]
    public void A_condition_can_hide_a_table()
    {
        var docx = TestDocx.With("((if Owes money))").RawParagraph(Table).Paragraph("((end if))").Paragraph("After.");

        Paragraphs(docx, """{ "owesMoney": false }""").Should().Equal("After.");
        Paragraphs(docx, """{ "owesMoney": true }""").Should().Equal("In the table", "After.");
    }

    [Fact]
    public void The_tags_may_be_written_in_any_case_and_stored_in_pieces()
    {
        var docx = new TestDocx().Paragraph("((IF  owes", " MONEY))").Paragraph("Shown.").Paragraph("((End ", "If))");

        Paragraphs(docx, """{ "owesMoney": true }""").Should().Equal("Shown.");
    }

    [Fact]
    public void A_field_used_only_in_hidden_content_needs_no_value()
    {
        var docx = TestDocx.With("((if Owes money))", "You owe ((Amount owed)).", "((end if))", "Bye.");

        Paragraphs(docx, """{ "owesMoney": false }""").Should().Equal("Bye.");
    }

    [Fact]
    public void A_field_used_in_shown_content_still_needs_a_value()
    {
        var docx = TestDocx.With("((if Owes money))", "You owe ((Amount owed)).", "((end if))");

        Render(docx, """{ "owesMoney": true }""").Diagnostics.Single().Code.Should().Be(DiagnosticCodes.ValueMissing);
    }

    [Fact]
    public void A_condition_with_no_value_is_reported()
    {
        var render = Render(TestDocx.With("((if Owes money))", "You owe us.", "((end if))"), "{}");

        render.Docx.Should().BeNull();
        render.Diagnostics.Single().Code.Should().Be(DiagnosticCodes.ValueMissing);
    }

    [Theory]
    [InlineData("\"yes\"")]
    [InlineData("1")]
    public void A_condition_whose_value_is_not_true_or_false_is_reported(string value)
    {
        var render = Render(TestDocx.With("((if Owes money))", "You owe us.", "((end if))"), $$"""{ "owesMoney": {{value}} }""");

        var diagnostic = render.Diagnostics.Single();
        diagnostic.Code.Should().Be(DiagnosticCodes.ValueWrongType);
        diagnostic.Fix.Should().Contain("true or false");
    }

    [Fact]
    public void A_condition_that_is_never_closed_is_reported_where_it_opens()
    {
        var diagnostic = Compile(TestDocx.With("Hello.", "((if Owes money))", "You owe us.")).Diagnostics.Single();

        diagnostic.Code.Should().Be(DiagnosticCodes.ConditionNotClosed);
        diagnostic.Location!.Paragraph.Should().Be(2);
    }

    [Fact]
    public void An_end_with_no_condition_open_is_reported()
    {
        var diagnostic = Compile(TestDocx.With("Hello.", "((end if))")).Diagnostics.Single();

        diagnostic.Code.Should().Be(DiagnosticCodes.EndWithoutCondition);
        diagnostic.Location!.Paragraph.Should().Be(2);
    }

    [Fact]
    public void A_condition_that_opens_in_a_table_and_closes_outside_it_is_reported()
    {
        var docx = new TestDocx()
            .RawParagraph("<w:tbl><w:tr><w:tc><w:p><w:r><w:t>((if Owes money))</w:t></w:r></w:p></w:tc></w:tr></w:tbl>")
            .Paragraph("((end if))");

        Compile(docx).Diagnostics.Select(d => d.Code).Should().Contain(DiagnosticCodes.ConditionNotClosed);
    }

    [Theory]
    [InlineData("((if Owes money)) You owe us.")]
    [InlineData("You owe us. ((end if))")]
    [InlineData("((if Owes money))((end if))")]
    public void A_condition_tag_that_shares_its_paragraph_is_reported(string paragraph)
    {
        var compilation = Compile(TestDocx.With("((if Owes money))", paragraph, "((end if))"));

        compilation.Diagnostics.Select(d => d.Code).Should().Contain(DiagnosticCodes.ConditionTagNotAlone);
    }

    [Fact]
    public void A_condition_on_a_field_that_is_not_yes_or_no_is_reported()
    {
        var diagnostic = Compile(TestDocx.With("((if Applicant name))", "Hello.", "((end if))")).Diagnostics.Single();

        diagnostic.Code.Should().Be(DiagnosticCodes.ConditionNeedsYesNo);
        diagnostic.Message.Should().Contain("Applicant name");
    }

    [Fact]
    public void A_condition_on_a_misspelt_field_suggests_the_nearest_one()
    {
        var diagnostic = Compile(TestDocx.With("((if Owes mony))", "Hello.", "((end if))")).Diagnostics.Single();

        diagnostic.Code.Should().Be(DiagnosticCodes.UnknownField);
        diagnostic.Fix.Should().Contain("((if Owes money))");
    }

    [Fact]
    public void One_mistake_in_a_condition_is_reported_once_not_again_at_its_end()
    {
        Compile(TestDocx.With("((if Owes mony))", "Hello.", "((end if))", "((if Applicant name))", "Hi.", "((end if))"))
            .Diagnostics.Select(d => d.Code).Should().Equal(DiagnosticCodes.UnknownField, DiagnosticCodes.ConditionNeedsYesNo);
    }

    [Fact]
    public void A_yes_or_no_field_cannot_be_printed_as_a_value()
    {
        var diagnostic = Compile(TestDocx.With("Owes money: ((Owes money))")).Diagnostics.Single();

        diagnostic.Code.Should().Be(DiagnosticCodes.YesNoPrinted);
        diagnostic.Fix.Should().Contain("((if Owes money))");
    }

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
