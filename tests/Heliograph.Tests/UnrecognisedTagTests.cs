using Heliograph.Documents;
using Heliograph.Tests.Support;

namespace Heliograph.Tests;

/// <summary>
/// The worst failure is a tag the engine does not see, because nothing fails: the letter goes out with the tag
/// printed in it. See docs/edge-cases.md, camp 1.
/// </summary>
public class UnrecognisedTagTests
{
    private const string Relationships = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
    private const string Footnotes = "<w:footnotes xmlns:w=\"http://schemas.openxmlformats.org/wordprocessingml/2006/main\">"
                                     + "<w:footnote w:id=\"1\"><w:p><w:r><w:t>Ref ((Applicant name))</w:t></w:r></w:p></w:footnote></w:footnotes>";

    private static readonly FieldDictionary Dictionary = new(
    [
        new Field("Applicant name", "applicantName", FieldType.Text),
        new Field("Member's start - end", "memberDates", FieldType.Text),
        new Field("Owes money", "owesMoney", FieldType.YesNo),
    ]);

    private static readonly string Data =
        """{ "applicantName": "Alex Applicant", "memberDates": "2020 to 2026", "owesMoney": true }""";

    [Theory]
    [InlineData("((Member’s start - end))")]
    [InlineData("((Member‘s start – end))")]
    [InlineData("((Member's start — end))")]
    [InlineData("((Mem­ber's start​ - end))")]
    [InlineData("((﻿Member's start - end⁠))")]
    public void A_name_the_editor_has_restyled_or_that_was_pasted_with_invisible_characters_is_still_found(string tag)
    {
        var compilation = Compile(TestDocx.With(tag));

        compilation.Diagnostics.Should().BeEmpty();
        Paragraphs(compilation).Should().Equal("2020 to 2026");
    }

    [Theory]
    [InlineData("Dear {{Applicant name}},")]
    [InlineData("Dear [[Applicant name]],")]
    [InlineData("Dear <<Applicant name>>,")]
    [InlineData("Dear «Applicant name»,")]
    [InlineData("Dear (Applicant name)),")]
    [InlineData("Dear (applicant  NAME),")]
    [InlineData("Dear （（Applicant name））,")]
    [InlineData("{{if Owes money}}")]
    public void Text_that_looks_like_a_tag_for_a_real_field_is_pointed_out_without_refusing_the_template(string paragraph)
    {
        var compilation = Compile(TestDocx.With("Hello.", paragraph));

        compilation.Succeeded.Should().BeTrue();
        var warning = compilation.Diagnostics.Should().ContainSingle().Subject;
        warning.Code.Should().Be(DiagnosticCodes.LooksLikeATag);
        warning.Severity.Should().Be(DiagnosticSeverity.Warning);
        warning.Location!.Paragraph.Should().Be(2);
        warning.Fix.Should().Contain("((");
    }

    [Theory]
    [InlineData("The applicant name is on the form.")]
    [InlineData("Dear ((Applicant name)), your applicant name is recorded.")]
    [InlineData("See paragraph 3(a)) and (b).")]
    [InlineData("Collect your (Applicant nameplate) and your (reapplicant name).")]
    public void Ordinary_text_that_mentions_a_field_is_left_alone(string paragraph)
    {
        Compile(TestDocx.With(paragraph)).Diagnostics.Should().BeEmpty();
    }

    [Fact]
    public void A_tag_in_a_footnote_is_filled_in()
    {
        var compilation = Compile(TestDocx.With("Hello.")
            .Part("word/footnotes.xml", Footnotes, "application/vnd.openxmlformats-officedocument.wordprocessingml.footnotes+xml")
            .Relationship($"{Relationships}/footnotes", "footnotes.xml", external: false));

        compilation.Diagnostics.Should().BeEmpty();
        var docx = compilation.Document!.Render(Benchmark.Data(Data), Benchmark.British).Docx!;
        TestDocx.PartParagraphs(docx, "word/footnotes.xml").Should().Equal("Ref Alex Applicant");
    }

    [Fact]
    public void A_problem_in_a_footnote_says_it_is_in_the_footnotes()
    {
        var compilation = Compile(TestDocx.With("Hello.")
            .Part("word/footnotes.xml", Footnotes.Replace("Applicant name", "Aplicant name", StringComparison.Ordinal))
            .Relationship($"{Relationships}/footnotes", "footnotes.xml", external: false));

        compilation.Diagnostics.Single().Location!.Part.Should().Be("footnotes");
    }

    [Fact]
    public void A_tag_in_a_pictures_description_is_reported_because_it_would_not_be_filled_in()
    {
        var docx = TestDocx.With("Hello.").RawParagraph(
            "<w:p><w:r><w:pict><v:shape xmlns:v=\"urn:schemas-microsoft-com:vml\" alt=\"Photo of ((Applicant name))\"/></w:pict></w:r></w:p>");

        NotFilledIn(docx).Message.Should().Contain("Applicant name");
    }

    [Fact]
    public void A_tag_in_a_fields_code_is_reported()
    {
        var docx = TestDocx.With("Hello.").RawParagraph(
            "<w:p><w:r><w:fldChar w:fldCharType=\"begin\"/></w:r><w:r><w:instrText> HYPERLINK \"https://www.example.org/((Applicant name))\" </w:instrText></w:r>"
            + "<w:r><w:fldChar w:fldCharType=\"separate\"/></w:r><w:r><w:t>our site</w:t></w:r><w:r><w:fldChar w:fldCharType=\"end\"/></w:r></w:p>");

        NotFilledIn(docx);
    }

    [Fact]
    public void A_tag_in_a_links_address_is_reported()
    {
        NotFilledIn(TestDocx.With("Hello.").Relationship($"{Relationships}/hyperlink", "https://www.example.org/((Applicant name))", external: true));
    }

    [Fact]
    public void A_tag_in_the_documents_title_is_reported()
    {
        NotFilledIn(TestDocx.With("Hello.").Author("Casey Caseworker", title: "Letter for ((Applicant name))"));
    }

    [Fact]
    public void A_condition_tag_somewhere_it_cannot_work_is_reported()
    {
        var docx = TestDocx.With("Hello.").RawParagraph(
            "<w:p><w:r><w:pict><v:shape xmlns:v=\"urn:schemas-microsoft-com:vml\" alt=\"((if Owes money))\"/></w:pict></w:r></w:p>");

        NotFilledIn(docx);
    }

    [Fact]
    public void Brackets_somewhere_tags_are_not_read_that_name_no_field_are_only_pointed_out()
    {
        var compilation = Compile(TestDocx.With("Hello.").RawParagraph(
            "<w:p><w:r><w:pict><v:shape xmlns:v=\"urn:schemas-microsoft-com:vml\" alt=\"f((x)) plotted\"/></w:pict></w:r></w:p>"));

        compilation.Succeeded.Should().BeTrue();
        compilation.Diagnostics.Single().Severity.Should().Be(DiagnosticSeverity.Warning);
    }

    [Fact]
    public void A_document_saved_as_Strict_Open_XML_is_refused_instead_of_compiling_with_no_tags_found()
    {
        var compilation = Compile(TestDocx.With("Dear ((Applicant name)),").WordNamespace("http://purl.oclc.org/ooxml/wordprocessingml/main"));

        compilation.Succeeded.Should().BeFalse();
        var diagnostic = compilation.Diagnostics.Should().ContainSingle().Subject;
        diagnostic.Code.Should().Be(DiagnosticCodes.StrictFormat);
        diagnostic.Fix.Should().Contain("Word Document (.docx)");
    }

    [Fact]
    public void An_old_or_password_protected_Word_file_is_refused_with_a_message_that_says_so()
    {
        using var stream = new MemoryStream([0xD0, 0xCF, 0x11, 0xE0, 0xA1, 0xB1, 0x1A, 0xE1, 0, 0, 0, 0]);

        var diagnostic = DocumentTemplate.Compile(stream, Dictionary).Diagnostics.Should().ContainSingle().Subject;

        diagnostic.Code.Should().Be(DiagnosticCodes.NotAWordDocument);
        diagnostic.Message.Should().Contain("password").And.Contain(".doc");
    }

    private static Diagnostic NotFilledIn(TestDocx docx)
    {
        var compilation = Compile(docx);
        compilation.Succeeded.Should().BeFalse();
        var diagnostic = compilation.Diagnostics.Should().ContainSingle().Subject;
        diagnostic.Code.Should().Be(DiagnosticCodes.TagNotFilledIn);
        return diagnostic;
    }

    private static DocumentCompilation Compile(TestDocx docx)
    {
        using var stream = docx.Build();
        return DocumentTemplate.Compile(stream, Dictionary);
    }

    private static IReadOnlyList<string> Paragraphs(DocumentCompilation compilation) =>
        TestDocx.Paragraphs(compilation.Document!.Render(Benchmark.Data(Data), Benchmark.British).Docx!);
}
