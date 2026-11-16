using System.IO.Compression;
using Heliograph.Documents;
using Heliograph.Tests.Support;

namespace Heliograph.Tests;

/// <summary>An uploaded file is untrusted. Each of these is refused before anything else reads it.</summary>
public class UnsafeFileTests
{
    private const string OfficeRelationships = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
    private static readonly FieldDictionary Dictionary = new([new Field("Applicant name", "applicantName", FieldType.Text)]);

    [Fact]
    public void A_file_that_is_not_a_Word_document_is_refused()
    {
        using var notADocument = new MemoryStream("Dear ((Applicant name))"u8.ToArray());

        Refusal(notADocument).Should().Be(DiagnosticCodes.NotAWordDocument);
    }

    [Fact]
    public void A_zip_that_holds_something_other_than_a_Word_document_is_refused()
    {
        var stream = new MemoryStream();
        using (var zip = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            using var writer = new StreamWriter(zip.CreateEntry("readme.txt").Open());
            writer.Write("hello");
        }

        stream.Position = 0;

        Refusal(stream).Should().Be(DiagnosticCodes.NotAWordDocument);
    }

    [Fact]
    public void A_document_with_macros_is_refused()
    {
        var docx = TestDocx.With("Dear ((Applicant name)),")
            .Part("word/vbaProject.bin", "macro", "application/vnd.ms-office.vbaProject");

        Refusal(docx).Should().Be(DiagnosticCodes.ContainsMacros);
    }

    [Fact]
    public void A_macro_enabled_document_is_refused_even_with_no_macro_in_it()
    {
        var docx = TestDocx.With("Dear ((Applicant name)),")
            .MainContentType("application/vnd.ms-word.document.macroEnabled.main+xml");

        Refusal(docx).Should().Be(DiagnosticCodes.ContainsMacros);
    }

    [Fact]
    public void A_document_with_an_embedded_object_is_refused()
    {
        var docx = TestDocx.With("Dear ((Applicant name)),")
            .Part("word/embeddings/oleObject1.bin", "object", "application/vnd.openxmlformats-officedocument.oleObject");

        Refusal(docx).Should().Be(DiagnosticCodes.ContainsEmbeddedObject);
    }

    [Fact]
    public void A_document_that_loads_a_picture_from_outside_the_file_is_refused()
    {
        var docx = TestDocx.With("Dear ((Applicant name)),")
            .Relationship($"{OfficeRelationships}/image", "https://tracker.example/pixel.png", external: true);

        Refusal(docx).Should().Be(DiagnosticCodes.LoadsOutsideContent);
    }

    [Fact]
    public void A_document_attached_to_a_template_outside_the_file_is_refused()
    {
        var docx = TestDocx.With("Dear ((Applicant name)),")
            .Relationship($"{OfficeRelationships}/attachedTemplate", "file://server/share/evil.dotm", external: true);

        Refusal(docx).Should().Be(DiagnosticCodes.LoadsOutsideContent);
    }

    [Fact]
    public void An_ordinary_link_to_a_web_page_is_allowed()
    {
        var docx = TestDocx.With("Dear ((Applicant name)),")
            .Relationship($"{OfficeRelationships}/hyperlink", "https://www.example.org/help", external: true);

        Compile(docx.Build()).Diagnostics.Should().BeEmpty();
    }

    [Fact]
    public void A_document_that_unpacks_to_more_than_the_limit_is_refused()
    {
        var docx = TestDocx.With("Dear ((Applicant name)),")
            .Part("word/media/padding.bin", new string('0', 70 * 1024 * 1024));

        Refusal(docx).Should().Be(DiagnosticCodes.TooLarge);
    }

    [Fact]
    public void A_document_that_declares_an_external_entity_is_refused()
    {
        var docx = TestDocx.With("Dear ((Applicant name)),")
            .Part("word/header9.xml", "<!DOCTYPE x [<!ENTITY e SYSTEM \"file:///etc/passwd\">]><x>&e;</x>")
            .Relationship($"{OfficeRelationships}/header", "header9.xml", external: false);

        Refusal(docx).Should().Be(DiagnosticCodes.NotAWordDocument);
    }

    [Fact]
    public void A_file_cannot_plant_a_placeholder_that_only_the_compiler_should_write()
    {
        var docx = TestDocx.With("Dear ((Applicant name)),").RawParagraph(
            "<w:p><w:r><hg:value xmlns:hg=\"urn:heliograph:compiled\" key=\"applicantName\"/></w:r></w:p>"
            + "<w:p><w:r><hg:value xmlns:hg=\"urn:heliograph:compiled\" key=\"notInTheDictionary\"/></w:r></w:p>");

        var document = Compile(docx.Build()).Document!;
        var render = document.Render(Benchmark.Data("""{ "applicantName": "Alex Applicant" }"""), Benchmark.British);

        TestDocx.Paragraphs(render.Docx!).Should().Equal("Dear Alex Applicant,", "", "");
    }

    [Fact]
    public void A_refused_file_gives_no_template_to_render()
    {
        using var notADocument = new MemoryStream([1, 2, 3]);

        var compilation = Compile(notADocument);

        compilation.Succeeded.Should().BeFalse();
        compilation.Document.Should().BeNull();
    }

    private static string Refusal(TestDocx docx) => Refusal(docx.Build());

    private static string Refusal(Stream stream) => Compile(stream).Diagnostics.Should().ContainSingle().Subject.Code;

    private static DocumentCompilation Compile(Stream stream)
    {
        using (stream)
        {
            return DocumentTemplate.Compile(stream, Dictionary);
        }
    }
}
