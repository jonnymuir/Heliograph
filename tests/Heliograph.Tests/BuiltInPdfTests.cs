using System.Text;
using System.Text.RegularExpressions;
using Heliograph.Documents;
using Heliograph.Pdf;
using Heliograph.Pdf.LibreOffice;
using Heliograph.Tests.Support;

namespace Heliograph.Tests;

/// <summary>
/// Heliograph's own PDF converter: accessible, deterministic, and laid out as LibreOffice lays out the same letter,
/// line for line, with nothing installed.
/// </summary>
public class BuiltInPdfTests
{
    public static TheoryData<string, string, string> Letters => new()
    {
        { "acknowledgement", "awkward-characters", "document.docx" },
        { "acknowledgement", "typical", "document.word-365.docx" },
        { "decision-letter", "approved", "document.docx" },
        { "decision-letter", "refused", "document.docx" },
        { "annual-statement", "weekly-payer", "document.docx" },
        { "annual-statement", "no-contributions", "document.docx" },
    };

    [VeraPdfTheory]
    [MemberData(nameof(Letters))]
    public async Task Every_benchmark_letter_passes_an_independent_check_for_accessible_pdf(string letter, string sample, string file)
    {
        var pdf = await new HeliographPdfConverter().ConvertAsync(Letter(letter, sample, file));

        PdfValidator.FailuresAsPdfUa(pdf).Should().BeEmpty();
    }

    [ComparedPdfTheory]
    [MemberData(nameof(Letters))]
    public async Task Every_benchmark_letter_is_laid_out_as_LibreOffice_lays_it_out(string letter, string sample, string file)
    {
        var docx = Letter(letter, sample, file);
        var expected = PdfLines.Read(await new LibreOfficePdfConverter().ConvertAsync(docx));
        var actual = PdfLines.Read(await new HeliographPdfConverter().ConvertAsync(docx));

        actual.Select(l => (l.Page, l.Text)).Should().Equal(expected.Select(l => (l.Page, l.Text)),
            "each line holds the same words, on the same page");
        actual.Zip(expected).Should().OnlyContain(pair => Math.Abs(pair.First.Top - pair.Second.Top) <= 1.5,
            "each line sits where LibreOffice puts it, to within a point and a half");
    }

    [Theory]
    [MemberData(nameof(Letters))]
    public async Task The_same_letter_always_gives_the_same_bytes(string letter, string sample, string file)
    {
        var docx = Letter(letter, sample, file);
        var converter = new HeliographPdfConverter();

        (await converter.ConvertAsync(docx)).Should().Equal(await converter.ConvertAsync(docx));
    }

    [Fact]
    public async Task A_long_statement_runs_over_the_same_three_pages()
    {
        var pdf = Encoding.Latin1.GetString(await new HeliographPdfConverter().ConvertAsync(Letter("annual-statement", "weekly-payer")));

        Regex.Matches(pdf, @"/Type /Page[^s]").Should().HaveCount(3);
        pdf.Should().Contain("/StructTreeRoot").And.Contain("pdfuaid:part>1");
    }

    [Fact]
    public async Task A_letter_longer_than_the_page_limit_is_refused()
    {
        var converting = () => new HeliographPdfConverter(new HeliographPdfOptions { MaxPages = 2 }).ConvertAsync(Letter("annual-statement", "weekly-payer"));

        (await converting.Should().ThrowAsync<PdfConversionException>()).Which.Message.Should().Contain("2 pages");
    }

    [Fact]
    public async Task A_conversion_that_is_cancelled_stops()
    {
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();

        var converting = () => new HeliographPdfConverter().ConvertAsync(Letter("decision-letter", "approved"), cancelled.Token);

        await converting.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task Something_that_is_not_a_Word_document_is_refused_without_repeating_it()
    {
        var converting = () => new HeliographPdfConverter().ConvertAsync(Encoding.UTF8.GetBytes("Alex Example, 1 Example Street"));

        var refused = (await converting.Should().ThrowAsync<PdfConversionException>()).Which;
        refused.Message.Should().NotContain("Alex");
    }

    private static byte[] Letter(string letter, string sample, string file = "document.docx")
    {
        var render = Benchmark.Compile(letter, file).Document!.Render(Benchmark.Sample(letter, sample), Benchmark.British);
        render.Diagnostics.Should().BeEmpty();
        return render.Docx!;
    }
}
