using Heliograph.Documents;
using Heliograph.Tests.Support;

namespace Heliograph.Tests;

/// <summary>What happens when the data for one recipient is wrong, or tries to be more than data.</summary>
public class RenderDataTests
{
    private static readonly DataDictionary Dictionary = new(
    [
        new DictionaryEntry("Applicant name", "applicantName", EntryType.Text),
        new DictionaryEntry("Event date", "eventDate", EntryType.Date),
        new DictionaryEntry("Applicant address", "applicantAddress", EntryType.Address),
        new DictionaryEntry("Notes", "notes", EntryType.Text),
    ]);

    [Fact]
    public void A_missing_value_is_reported_and_no_letter_is_produced()
    {
        var render = Render("Dear ((Applicant name)),", "{}");

        render.Docx.Should().BeNull();
        var diagnostic = render.Diagnostics.Should().ContainSingle().Subject;
        diagnostic.Code.Should().Be(DiagnosticCodes.ValueMissing);
        diagnostic.Message.Should().Contain("Applicant name");
        diagnostic.Fix.Should().Contain("applicantName");
    }

    [Fact]
    public void A_null_value_counts_as_missing()
    {
        Render("Dear ((Applicant name)),", """{ "applicantName": null }""")
            .Diagnostics.Single().Code.Should().Be(DiagnosticCodes.ValueMissing);
    }

    [Fact]
    public void A_value_the_template_does_not_use_may_be_left_out()
    {
        Render("Dear ((Applicant name)),", """{ "applicantName": "Alex Applicant" }""").Diagnostics.Should().BeEmpty();
    }

    [Theory]
    [InlineData("""{ "eventDate": "19 June 2027" }""")]
    [InlineData("""{ "eventDate": "2027-13-45" }""")]
    [InlineData("""{ "eventDate": 20270619 }""")]
    public void A_date_that_is_not_an_ISO_date_is_reported(string data)
    {
        var diagnostic = Render("On ((Event date)).", data).Diagnostics.Should().ContainSingle().Subject;

        diagnostic.Code.Should().Be(DiagnosticCodes.ValueWrongType);
        diagnostic.Fix.Should().Contain("2027-06-19");
    }

    [Theory]
    [InlineData("""{ "applicantName": 42 }""")]
    [InlineData("""{ "applicantName": ["Alex"] }""")]
    public void Text_that_is_not_text_is_reported(string data)
    {
        Render("Dear ((Applicant name)),", data).Diagnostics.Single().Code.Should().Be(DiagnosticCodes.ValueWrongType);
    }

    [Theory]
    [InlineData("""{ "applicantAddress": "12 Tumble Lane, Clubtown" }""")]
    [InlineData("""{ "applicantAddress": ["12 Tumble Lane", 7] }""")]
    public void An_address_that_is_not_a_list_of_lines_is_reported(string data)
    {
        Render("((Applicant address))", data).Diagnostics.Single().Code.Should().Be(DiagnosticCodes.ValueWrongType);
    }

    [Fact]
    public void Text_that_looks_like_a_field_stays_text()
    {
        var paragraphs = Paragraphs("Dear ((Applicant name)), on ((Event date)).",
            """{ "applicantName": "((Event date))", "eventDate": "2027-06-19" }""");

        paragraphs.Should().Contain("Dear ((Event date)), on 19 June 2027.");
    }

    [Fact]
    public void Text_that_looks_like_markup_stays_text()
    {
        var paragraphs = Paragraphs("((Notes))",
            """{ "notes": "</w:t></w:r><w:r><w:rPr><w:b/></w:rPr><w:t>BOLD & <b>bold</b>" }""");

        paragraphs.Should().Equal("</w:t></w:r><w:r><w:rPr><w:b/></w:rPr><w:t>BOLD & <b>bold</b>");
    }

    [Fact]
    public void Characters_a_Word_document_cannot_hold_are_dropped_rather_than_breaking_the_file()
    {
        Paragraphs("((Notes))", """{ "notes": "bell\u0007 and null\u0000 gone" }""").Should().Equal("bell and null gone");
    }

    [Fact]
    public void Paragraphs_in_supplied_text_become_line_breaks()
    {
        Paragraphs("((Notes))", """{ "notes": "First line.\nSecond line.\r\nThird line." }""")
            .Should().Equal("First line.\nSecond line.\nThird line.");
    }

    [Fact]
    public void A_leading_space_in_supplied_text_is_kept()
    {
        Paragraphs("Dear((Applicant name)),", """{ "applicantName": " Alex " }""").Should().Equal("Dear Alex ,");
    }

    private static DocumentRender Render(string paragraph, string data)
    {
        using var stream = TestDocx.With(paragraph).Build();
        var compilation = DocumentTemplate.Compile(stream, Dictionary);
        compilation.Diagnostics.Should().BeEmpty();
        return compilation.Document!.Render(Benchmark.Data(data), Benchmark.British);
    }

    private static IReadOnlyList<string> Paragraphs(string paragraph, string data)
    {
        var render = Render(paragraph, data);
        render.Diagnostics.Should().BeEmpty();
        return TestDocx.Paragraphs(render.Docx!);
    }
}
