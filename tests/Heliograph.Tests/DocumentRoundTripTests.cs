using System.Xml.Linq;
using Heliograph.Documents;
using Heliograph.Library;
using Heliograph.Tests.Support;

namespace Heliograph.Tests;

/// <summary>
/// The steps an author takes with a document: start from a starter document, upload it, get it back with a comment
/// at each problem, fix it and upload it again. Each step works in a stock word processor.
/// </summary>
public class DocumentRoundTripTests
{
    private static readonly XNamespace Word = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";

    private static readonly FieldDictionary Dictionary = FieldDictionary.Parse("""
        { "fields": [
          { "name": "Member name", "key": "memberName", "type": "text" },
          { "name": "Owes money", "key": "owesMoney", "type": "yesNo" },
          { "name": "Payments", "key": "payments", "type": "list", "fields": [ { "name": "Payment date", "key": "date", "type": "date" } ] }
        ] }
        """);

    private static byte[] Starter => StarterDocument.Create("Your annual statement", DictionarySheet.From("members", Dictionary));

    [Fact]
    public void A_starter_document_is_a_valid_Word_file_with_its_title_and_every_tag_to_copy()
    {
        var starter = Starter;

        WordSchema.Problems(starter).Should().BeEmpty();
        WordRules.Problems(starter).Should().BeEmpty();
        TestDocx.Property(starter, "title").Should().Be("Your annual statement");
        TestDocx.TableRows(starter).Select(row => row[0]).Should().Equal("Tag", "((Member name))", "((if Owes money))", "((Payment date))");
        TestDocx.ParagraphStyles(starter).Should().Contain("Heading1");
    }

    [Fact]
    public void A_starter_document_with_its_tag_sheet_still_in_is_refused_with_that_alone()
    {
        var compilation = DocumentTemplate.Compile(new MemoryStream(Starter), Dictionary);

        compilation.Succeeded.Should().BeFalse();
        var problem = compilation.Diagnostics.Should().ContainSingle().Subject;
        problem.Code.Should().Be(DiagnosticCodes.TagSheetLeftIn);
        problem.Fix.Should().Contain(StarterDocument.TagSheetMarker);
    }

    [Fact]
    public void A_starter_document_with_the_tag_sheet_deleted_and_a_letter_written_compiles_cleanly()
    {
        var letter = TestDocx.WithPart(Starter, "word/document.xml", document =>
        {
            var body = document.Descendants(Word + "body").Single();
            body.Elements().Where(e => e.Name != Word + "sectPr").Remove();
            body.AddFirst(new XElement(Word + "p", new XElement(Word + "r", new XElement(Word + "t", "Dear ((Member name)),"))));
        });

        DocumentTemplate.Compile(new MemoryStream(letter), Dictionary).Diagnostics.Should().BeEmpty();
    }

    [EditorFact]
    public void A_starter_document_saved_by_LibreOffice_still_has_its_title_and_its_tag_sheet_is_still_caught()
    {
        var saved = LibreOffice.OpenAndSave(Starter);

        TestDocx.Property(saved, "title").Should().Be("Your annual statement");
        DocumentTemplate.Compile(new MemoryStream(saved), Dictionary).Diagnostics.Should().ContainSingle()
            .Which.Code.Should().Be(DiagnosticCodes.TagSheetLeftIn);
    }

    [Fact]
    public void The_authors_document_comes_back_with_a_comment_on_the_paragraph_that_has_the_problem()
    {
        var template = TestDocx.With("Dear ((Member name)),", "You owe ((Amount owed)).").Build().ToArray();
        var diagnostics = DocumentTemplate.Compile(new MemoryStream(template), Dictionary).Diagnostics;

        var annotated = HeliographComments.Annotate(template, diagnostics)!;

        WordSchema.Problems(annotated).Should().BeEmpty();
        var comment = Comments(annotated).Should().ContainSingle().Subject;
        comment.Author.Should().Be(HeliographComments.Author);
        comment.Text.Should().Contain("Amount owed").And.Contain(DiagnosticCodes.UnknownField);
        CommentedParagraphs(annotated).Should().Equal("You owe ((Amount owed)).");
    }

    [Fact]
    public void Heliographs_comments_come_out_when_the_document_is_uploaded_again()
    {
        var template = TestDocx.With("Dear ((Member nam)),").Build().ToArray();
        var annotated = HeliographComments.Annotate(template, DocumentTemplate.Compile(new MemoryStream(template), Dictionary).Diagnostics)!;

        var fixedTemplate = Replace(annotated, "((Member nam))", "((Member name))");
        var compilation = DocumentTemplate.Compile(new MemoryStream(fixedTemplate), Dictionary);

        compilation.Diagnostics.Should().BeEmpty("the comments are Heliograph's own, not an author's remarks");
        var letter = compilation.Document!.Render(Benchmark.Data("""{ "memberName": "Alex" }"""), Benchmark.British).Docx!;
        Comments(letter).Should().BeEmpty();
        TestDocx.PartText(letter, "word/document.xml").Should().NotContain("commentReference");
    }

    [EditorFact]
    public void Heliographs_comments_still_come_out_after_LibreOffice_has_saved_the_document()
    {
        var template = TestDocx.With("Dear ((Member nam)),").Build().ToArray();
        var annotated = HeliographComments.Annotate(template, DocumentTemplate.Compile(new MemoryStream(template), Dictionary).Diagnostics)!;

        var saved = LibreOffice.OpenAndSave(annotated);

        Comments(saved).Should().ContainSingle().Which.Author.Should().Be(HeliographComments.Author);
        DocumentTemplate.Compile(new MemoryStream(saved), Dictionary).Diagnostics.Select(d => d.Code)
            .Should().Equal(DiagnosticCodes.UnknownField);
    }

    [Fact]
    public void Someone_elses_comment_is_still_refused_when_Heliographs_are_there_too()
    {
        var template = TestDocx.With("Dear ((Member nam)),").Build().ToArray();
        var annotated = HeliographComments.Annotate(template, DocumentTemplate.Compile(new MemoryStream(template), Dictionary).Diagnostics)!;
        var withRemark = TestDocx.WithPart(annotated, "word/comments.xml", comments => comments.Root!.Add(
            new XElement(Word + "comment", new XAttribute(Word + "id", "50"), new XAttribute(Word + "author", "Casey Caseworker"),
                new XElement(Word + "p", new XElement(Word + "r", new XElement(Word + "t", "Is this the difficult one?"))))));

        DocumentTemplate.Compile(new MemoryStream(withRemark), Dictionary).Diagnostics.Select(d => d.Code)
            .Should().Contain(DiagnosticCodes.HasComments);
    }

    [Fact]
    public void A_problem_in_a_header_is_commented_on_the_first_paragraph_and_says_where_it_is()
    {
        var template = TestDocx.With("Dear ((Member name)),").Header("Ref ((Reference))").Build().ToArray();

        var annotated = HeliographComments.Annotate(template, DocumentTemplate.Compile(new MemoryStream(template), Dictionary).Diagnostics)!;

        Comments(annotated).Should().ContainSingle().Which.Text.Should().StartWith("In the header, paragraph 1");
        CommentedParagraphs(annotated).Should().Equal("Dear ((Member name)),");
    }

    [Fact]
    public void Something_that_is_not_a_Word_document_cannot_be_annotated()
    {
        HeliographComments.Annotate("Dear Alex"u8.ToArray(), []).Should().BeNull();
    }

    private static List<(string Author, string Text)> Comments(byte[] docx)
    {
        using var zip = new System.IO.Compression.ZipArchive(new MemoryStream(docx));
        var entry = zip.Entries.FirstOrDefault(e => e.FullName.EndsWith("comments.xml", StringComparison.Ordinal));
        if (entry is null)
        {
            return [];
        }

        using var stream = entry.Open();
        return [.. XDocument.Load(stream).Descendants(Word + "comment")
            .Select(c => ((string?)c.Attribute(Word + "author") ?? "", string.Concat(c.Descendants(Word + "t").Select(t => t.Value))))];
    }

    private static List<string> CommentedParagraphs(byte[] docx) => [.. XDocument.Parse(TestDocx.PartText(docx, "word/document.xml"))
        .Descendants(Word + "p").Where(p => p.Descendants(Word + "commentReference").Any())
        .Select(p => string.Concat(p.Descendants(Word + "t").Select(t => t.Value)))];

    private static byte[] Replace(byte[] docx, string from, string to) => TestDocx.WithPart(docx, "word/document.xml", document =>
    {
        foreach (var text in document.Descendants(Word + "t").Where(t => t.Value.Contains(from, StringComparison.Ordinal)))
        {
            text.Value = text.Value.Replace(from, to, StringComparison.Ordinal);
        }
    });
}
