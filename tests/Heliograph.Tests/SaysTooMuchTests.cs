using Heliograph.Documents;
using Heliograph.Tests.Support;

namespace Heliograph.Tests;

/// <summary>
/// Whatever a template carries is copied to every recipient. Comments, tracked changes and the names of staff must
/// not travel. See docs/edge-cases.md, camp 3.
/// </summary>
public class SaysTooMuchTests
{
    private const string Relationships = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
    private const string Word = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";

    private static readonly FieldDictionary Dictionary = new([new Field("Applicant name", "applicantName", FieldType.Text)]);

    [Theory]
    [InlineData("<w:ins w:id=\"1\" w:author=\"Casey Caseworker\"><w:r><w:t>added</w:t></w:r></w:ins>")]
    [InlineData("<w:del w:id=\"1\" w:author=\"Casey Caseworker\"><w:r><w:delText>we think you are lying</w:delText></w:r></w:del>")]
    [InlineData("<w:moveFrom w:id=\"1\" w:author=\"Casey Caseworker\"><w:r><w:t>moved</w:t></w:r></w:moveFrom>")]
    [InlineData("<w:r><w:rPr><w:rPrChange w:id=\"1\" w:author=\"Casey Caseworker\"><w:rPr/></w:rPrChange></w:rPr><w:t>reformatted</w:t></w:r>")]
    public void A_template_with_tracked_changes_still_in_it_is_refused(string change)
    {
        var diagnostic = Refusal(TestDocx.With("Dear ((Applicant name)),").RawParagraph($"<w:p>{change}</w:p>"));

        diagnostic.Code.Should().Be(DiagnosticCodes.HasTrackedChanges);
        diagnostic.Fix.Should().Contain("Accept or reject");
    }

    [Fact]
    public void A_template_with_a_comment_in_it_is_refused()
    {
        var docx = TestDocx.With("Dear ((Applicant name)),")
            .Part("word/comments.xml", $"<w:comments xmlns:w=\"{Word}\"><w:comment w:id=\"0\" w:author=\"Casey Caseworker\">"
                                       + "<w:p><w:r><w:t>Is this applicant the difficult one?</w:t></w:r></w:p></w:comment></w:comments>")
            .Relationship($"{Relationships}/comments", "comments.xml", external: false);

        Refusal(docx).Code.Should().Be(DiagnosticCodes.HasComments);
    }

    [Fact]
    public void A_template_with_a_comment_mark_and_no_comment_text_is_refused_too()
    {
        var docx = TestDocx.With("Dear ((Applicant name)),").RawParagraph("<w:p><w:r><w:commentReference w:id=\"0\"/></w:r></w:p>");

        Refusal(docx).Code.Should().Be(DiagnosticCodes.HasComments);
    }

    [Fact]
    public void A_digitally_signed_template_is_refused_because_filling_it_in_breaks_the_signature()
    {
        var docx = TestDocx.With("Dear ((Applicant name)),").Part("_xmlsignatures/sig1.xml", "<Signature/>");

        Refusal(docx).Code.Should().Be(DiagnosticCodes.IsSigned);
    }

    [Fact]
    public void Hidden_text_is_pointed_out_because_it_is_sent_inside_the_file()
    {
        var compilation = Compile(TestDocx.With("Dear ((Applicant name)),")
            .RawParagraph("<w:p><w:r><w:rPr><w:vanish/></w:rPr><w:t>internal note</w:t></w:r></w:p>"));

        compilation.Succeeded.Should().BeTrue();
        var warning = compilation.Diagnostics.Should().ContainSingle().Subject;
        warning.Code.Should().Be(DiagnosticCodes.HasHiddenText);
        warning.Severity.Should().Be(DiagnosticSeverity.Warning);
        warning.Location!.Paragraph.Should().Be(2);
    }

    [Fact]
    public void Text_with_hiding_switched_off_is_not_hidden_text()
    {
        Compile(TestDocx.With("Hello.").RawParagraph("<w:p><w:r><w:rPr><w:vanish w:val=\"0\"/></w:rPr><w:t>shown</w:t></w:r></w:p>"))
            .Diagnostics.Should().BeEmpty();
    }

    [Fact]
    public void The_names_of_people_who_worked_on_the_template_are_not_copied_into_the_letter()
    {
        var docx = Render(TestDocx.With("Dear ((Applicant name)),")
            .Part("word/people.xml", "<w15:people xmlns:w15=\"http://schemas.microsoft.com/office/word/2012/wordml\">"
                                     + "<w15:person w15:author=\"Casey Caseworker\"/></w15:people>")
            .Relationship("http://schemas.microsoft.com/office/2011/relationships/people", "people.xml", external: false));

        TestDocx.PartText(docx, "word/people.xml").Should().NotContain("Casey");
    }

    [Fact]
    public void The_company_and_manager_recorded_in_the_template_are_not_copied_into_the_letter()
    {
        var docx = Render(TestDocx.With("Dear ((Applicant name)),")
            .Part("docProps/app.xml", "<Properties xmlns=\"http://schemas.openxmlformats.org/officeDocument/2006/extended-properties\">"
                                      + "<Application>Microsoft Office Word</Application><Company>Fraud Investigations Unit</Company>"
                                      + "<Manager>Casey Caseworker</Manager></Properties>")
            .RootRelationship($"{Relationships}/extended-properties", "docProps/app.xml"));

        var properties = TestDocx.PartText(docx, "docProps/app.xml");
        properties.Should().NotContain("Fraud").And.NotContain("Casey");
        properties.Should().Contain("Microsoft Office Word");
    }

    [Fact]
    public void The_benchmark_template_a_person_saved_in_Microsoft_365_has_none_of_these()
    {
        Benchmark.Compile("acknowledgement", "document.word-365.docx").Diagnostics
            .Should().OnlyContain(diagnostic => diagnostic.Code == DiagnosticCodes.DocumentHasNoTitle);
    }

    private static Diagnostic Refusal(TestDocx docx)
    {
        var compilation = Compile(docx);
        compilation.Succeeded.Should().BeFalse();
        return compilation.Diagnostics.Should().ContainSingle().Subject;
    }

    private static DocumentCompilation Compile(TestDocx docx)
    {
        using var stream = docx.Build();
        return DocumentTemplate.Compile(stream, Dictionary);
    }

    private static byte[] Render(TestDocx docx)
    {
        var compilation = Compile(docx);
        compilation.Diagnostics.Should().BeEmpty();
        return compilation.Document!.Render(Benchmark.Data("""{ "applicantName": "Alex Applicant" }"""), Benchmark.British).Docx!;
    }
}
