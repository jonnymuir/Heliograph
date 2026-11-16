using Heliograph.Documents;
using Heliograph.Tests.Support;

namespace Heliograph.Tests;

/// <summary>What an author is told when a template is wrong, and what is quietly accepted.</summary>
public class CompileDiagnosticsTests
{
    private static readonly FieldDictionary Dictionary = new(
    [
        new Field("Applicant name", "applicantName", FieldType.Text),
        new Field("Event date", "eventDate", FieldType.Date),
    ]);

    [Fact]
    public void A_misspelt_field_is_reported_with_the_nearest_field_in_the_dictionary()
    {
        var compilation = Compile(TestDocx.With("Hello.", "Dear ((Aplicant name)),"));

        compilation.Succeeded.Should().BeFalse();
        var diagnostic = compilation.Diagnostics.Should().ContainSingle().Subject;
        diagnostic.Code.Should().Be(DiagnosticCodes.UnknownField);
        diagnostic.Message.Should().Contain("Aplicant name");
        diagnostic.Fix.Should().Contain("((Applicant name))");
    }

    [Fact]
    public void A_problem_says_where_in_the_document_it_is()
    {
        var location = Compile(TestDocx.With("Hello.", "Dear ((Aplicant name)),")).Diagnostics.Single().Location!;

        location.Part.Should().Be("document");
        location.Paragraph.Should().Be(2);
        location.Excerpt.Should().Be("Dear ((Aplicant name)),");
    }

    [Fact]
    public void A_problem_in_the_header_says_it_is_in_the_header()
    {
        var compilation = Compile(TestDocx.With("Hello.").Header("For ((Nobody))"));

        compilation.Diagnostics.Single().Location!.Part.Should().Be("header");
    }

    [Fact]
    public void A_field_nothing_like_any_in_the_dictionary_is_reported_without_a_guess()
    {
        var diagnostic = Compile(TestDocx.With("((Shoe size))")).Diagnostics.Single();

        diagnostic.Code.Should().Be(DiagnosticCodes.UnknownField);
        diagnostic.Fix.Should().NotContain("((");
    }

    [Fact]
    public void A_tag_that_is_opened_and_never_closed_is_reported()
    {
        var diagnostic = Compile(TestDocx.With("Dear ((Applicant name,")).Diagnostics.Single();

        diagnostic.Code.Should().Be(DiagnosticCodes.TagNotClosed);
    }

    [Fact]
    public void A_tag_with_nothing_in_it_is_reported()
    {
        var diagnostic = Compile(TestDocx.With("Dear (( )),")).Diagnostics.Single();

        diagnostic.Code.Should().Be(DiagnosticCodes.TagHasNoName);
    }

    [Fact]
    public void Every_problem_is_reported_not_just_the_first()
    {
        var compilation = Compile(TestDocx.With("((One))", "Fine.", "((Two)) and ((Three))"));

        compilation.Diagnostics.Should().HaveCount(3);
    }

    [Fact]
    public void Capitals_and_extra_spaces_in_a_field_name_do_not_matter()
    {
        var compilation = Compile(TestDocx.With("Dear ((  applicant   NAME )),"));

        compilation.Diagnostics.Should().BeEmpty();
        Render(compilation).Should().Contain("Dear Alex Applicant,");
    }

    [Fact]
    public void A_tag_an_editor_has_stored_in_several_pieces_is_still_read()
    {
        var compilation = Compile(new TestDocx().Paragraph("Dear ((Appli", "cant na", "me)),"));

        compilation.Diagnostics.Should().BeEmpty();
        Render(compilation).Should().Contain("Dear Alex Applicant,");
    }

    [Fact]
    public void Two_fields_side_by_side_are_both_filled_in()
    {
        var compilation = Compile(TestDocx.With("((Applicant name))((Event date))"));

        Render(compilation).Should().Contain("Alex Applicant19 June 2027");
    }

    [Fact]
    public void Text_around_a_field_is_kept()
    {
        var compilation = Compile(new TestDocx().Paragraph("Before ", "((Applicant name)) after, and on ", "((Event date))", "."));

        Render(compilation).Should().Contain("Before Alex Applicant after, and on 19 June 2027.");
    }

    private static DocumentCompilation Compile(TestDocx docx)
    {
        using var stream = docx.Build();
        return DocumentTemplate.Compile(stream, Dictionary);
    }

    private static IReadOnlyList<string> Render(DocumentCompilation compilation)
    {
        var data = Benchmark.Data("""{ "applicantName": "Alex Applicant", "eventDate": "2027-06-19" }""");
        return TestDocx.Paragraphs(compilation.Document!.Render(data, Benchmark.British).Docx!);
    }
}
