using System.Text;
using System.Text.RegularExpressions;
using Heliograph.Documents;
using Heliograph.Pdf.LibreOffice;
using Heliograph.Tests.Support;

namespace Heliograph.Tests;

/// <summary>A rendered letter becomes a PDF a screen reader can follow. These need LibreOffice.</summary>
public class PdfTests
{
    [EditorFact]
    public async Task A_rendered_letter_becomes_a_tagged_pdf_that_says_its_language()
    {
        var pdf = Latin1(await new LibreOfficePdfConverter().ConvertAsync(Letter("decision-letter", "approved-dangerous-props")));

        pdf.Should().StartWith("%PDF-");
        pdf.Should().Contain("/StructTreeRoot", "a tagged PDF has a structure tree");
        pdf.Should().Contain("/Lang").And.Contain("en-GB");
        pdf.Should().Contain("pdfuaid", "it declares itself PDF/UA");
    }

    [ValidatedPdfTheory]
    [InlineData("acknowledgement", "awkward-characters")]
    [InlineData("decision-letter", "approved")]
    [InlineData("decision-letter", "refused")]
    [InlineData("annual-statement", "weekly-payer")]
    [InlineData("annual-statement", "no-contributions")]
    public async Task Every_benchmark_letter_passes_an_independent_check_for_accessible_pdf(string letter, string sample)
    {
        var pdf = await new LibreOfficePdfConverter().ConvertAsync(Letter(letter, sample));

        PdfValidator.FailuresAsPdfUa(pdf).Should().BeEmpty();
    }

    [Fact]
    public void A_document_with_no_title_is_pointed_out_when_it_is_compiled_and_still_compiles()
    {
        var dictionary = new FieldDictionary([new Field("Member name", "memberName", FieldType.Text)]);

        var compilation = DocumentTemplate.Compile(TestDocx.With("Dear ((Member name)),").Untitled().Build(), dictionary);

        compilation.Succeeded.Should().BeTrue();
        var warning = compilation.Diagnostics.Should().ContainSingle().Subject;
        (warning.Code, warning.Severity).Should().Be((DiagnosticCodes.DocumentHasNoTitle, DiagnosticSeverity.Warning));
        warning.Fix.Should().Contain("File > Properties").And.Contain("File > Info");
    }

    [Fact]
    public void A_document_with_a_title_is_not_warned_about_and_its_letters_keep_the_title()
    {
        var dictionary = new FieldDictionary([new Field("Member name", "memberName", FieldType.Text)]);

        var compilation = DocumentTemplate.Compile(TestDocx.With("Dear ((Member name)),").Author("Casey Caseworker", title: "Your annual statement").Build(), dictionary);

        compilation.Diagnostics.Should().BeEmpty();
        var render = compilation.Document!.Render(Benchmark.Data("""{ "memberName": "Alex" }"""), Benchmark.British);
        TestDocx.Property(render.Docx!, "title").Should().Be("Your annual statement");
    }

    [EditorFact]
    public async Task A_long_statement_runs_over_several_pages()
    {
        var pdf = Latin1(await new LibreOfficePdfConverter().ConvertAsync(Letter("annual-statement", "weekly-payer")));

        Regex.Matches(pdf, @"/Type\s*/Page[^s]").Should().HaveCount(3);
    }

    [EditorFact]
    public async Task Converting_leaves_nothing_behind_in_the_temporary_folder()
    {
        var before = Directory.GetDirectories(Path.GetTempPath(), "heliograph-pdf-*").Length;

        await new LibreOfficePdfConverter().ConvertAsync(Letter("acknowledgement", "typical"));

        Directory.GetDirectories(Path.GetTempPath(), "heliograph-pdf-*").Should().HaveCount(before);
    }

    [EditorFact]
    public async Task Letters_can_be_converted_side_by_side()
    {
        var converter = new LibreOfficePdfConverter();
        var letter = Letter("acknowledgement", "typical");

        var pdfs = await Task.WhenAll(Enumerable.Range(0, 3).Select(_ => converter.ConvertAsync(letter)));

        pdfs.Should().OnlyContain(pdf => Latin1(pdf).StartsWith("%PDF-", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Something_that_is_not_a_document_is_refused_without_quoting_it()
    {
        var convert = () => new LibreOfficePdfConverter().ConvertAsync(Encoding.UTF8.GetBytes("Dear Alex Applicant, this is not a Word file."));

        (await convert.Should().ThrowAsync<PdfConversionException>()).Which.Message.Should().NotContain("Alex");
    }

    [Fact]
    public async Task A_LibreOffice_that_cannot_be_started_is_reported_as_a_failed_conversion()
    {
        var converter = new LibreOfficePdfConverter(new LibreOfficeOptions { ExecutablePath = Path.Combine(Path.GetTempPath(), "no-such-soffice") });

        var convert = () => converter.ConvertAsync(Letter("acknowledgement", "typical"));

        await convert.Should().ThrowAsync<PdfConversionException>();
    }

    [EditorFact]
    public async Task A_conversion_that_takes_too_long_is_stopped()
    {
        var converter = new LibreOfficePdfConverter(new LibreOfficeOptions { Timeout = TimeSpan.FromMilliseconds(1) });

        var convert = () => converter.ConvertAsync(Letter("annual-statement", "weekly-payer"));

        (await convert.Should().ThrowAsync<PdfConversionException>()).Which.Message.Should().Contain("stopped");
    }

    private static byte[] Letter(string letter, string sample)
    {
        var render = Benchmark.Compile(letter).Document!.Render(Benchmark.Sample(letter, sample), Benchmark.British);
        render.Diagnostics.Should().BeEmpty();
        return render.Docx!;
    }

    private static string Latin1(byte[] pdf) => Encoding.Latin1.GetString(pdf);
}
