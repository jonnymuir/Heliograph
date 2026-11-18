using System.Text;
using System.Text.Json;
using Heliograph.Documents;
using Heliograph.Pdf;
using Heliograph.Pdf.LibreOffice;
using Heliograph.Tests.Support;

namespace Heliograph.Tests;

/// <summary>
/// Endnotes in Heliograph's own PDF converter: numbered i, ii, iii in the order they are referred to, after the letter's
/// text under the separator, carrying on to further pages, beside footnotes, and numbered as the document's settings say,
/// laid out as LibreOffice does.
/// </summary>
public class PdfEndnoteTests
{
    private const string Long = "Charged on fees paid after the end of April, which is a long note that runs on to a second line of note text.";
    private static readonly FieldDictionary NoFields = new([]);

    public static TheoryData<string> Letters => ["two endnotes", "endnotes on a later page", "endnotes running over a page", "with footnotes", "numbered as the settings say", "numbered as the settings say, with footnotes"];

    [ComparedPdfTheory]
    [MemberData(nameof(Letters))]
    public async Task Endnotes_are_laid_out_as_LibreOffice_lays_them_out(string kind)
    {
        var docx = Rendered(Letter(kind));
        var expected = PdfLines.Read(await new LibreOfficePdfConverter().ConvertAsync(docx));
        var actual = PdfLines.Read(await new HeliographPdfConverter().ConvertAsync(docx));

        PdfComparison.FirstDifference(expected, actual, "LibreOffice").Difference.Should().BeNull(kind);
    }

    [PdfLinesFact]
    public async Task Endnotes_are_numbered_on_their_own_after_the_text()
    {
        var lines = PdfLines.Read(await new HeliographPdfConverter().ConvertAsync(Rendered(Letter("with footnotes"))));

        lines.Should().Contain(l => l.Text.EndsWith("April. i", StringComparison.Ordinal)).And.Contain(l => l.Text.EndsWith("pounds. 1", StringComparison.Ordinal));
        lines.FindIndex(l => l.Text.StartsWith("i Membership", StringComparison.Ordinal)).Should().BeGreaterThan(lines.FindIndex(l => l.Text.StartsWith("We look forward", StringComparison.Ordinal)));
    }

    [VeraPdfTheory]
    [InlineData("two endnotes")]
    [InlineData("endnotes running over a page")]
    public async Task A_letter_with_endnotes_is_still_an_accessible_PDF(string kind)
    {
        var pdf = await new HeliographPdfConverter().ConvertAsync(Rendered(Letter(kind)));

        Encoding.Latin1.GetString(pdf).Should().Contain("/ID (endnote-1)");
        PdfValidator.FailuresAsPdfUa(pdf).Should().BeEmpty();
    }

    [Theory]
    [InlineData("<w:endnotePr><w:numRestart w:val=\"eachSect\"/></w:endnotePr>", "numbered or placed")]
    [InlineData("<w:endnotePr><w:pos w:val=\"sectEnd\"/></w:endnotePr>", "numbered or placed")]
    [InlineData("<w:footnotePr><w:numFmt w:val=\"chicago\"/></w:footnotePr>", "numbered or placed")]
    public void Notes_numbered_or_placed_some_other_way_are_refused_at_compile(string settings, string named)
    {
        var docx = NoteLetters.Letter([NoteLetters.EndReferring("Your membership renews in April.", 1)], [], ["Membership runs from April to March."], settings);

        DocumentTemplate.Compile(docx, NoFields, [new HeliographPdfConverter()]).Diagnostics
            .Should().ContainSingle(d => d.Code == DiagnosticCodes.PdfCannotLayOutNotes).Which.Message.Should().Contain(named);
    }

    [Fact]
    public void Sections_that_number_notes_differently_are_refused_at_compile()
    {
        const string W = "xmlns:w=\"http://schemas.openxmlformats.org/wordprocessingml/2006/main\"";
        var body = new[]
        {
            $"<w:p {W}><w:pPr><w:sectPr><w:endnotePr><w:numFmt w:val=\"decimal\"/></w:endnotePr></w:sectPr></w:pPr><w:r><w:t>First part</w:t></w:r></w:p>",
            NoteLetters.EndReferring("Your membership renews in April.", 1),
            $"<w:sectPr {W}><w:endnotePr><w:numFmt w:val=\"upperRoman\"/></w:endnotePr></w:sectPr>",
        };

        DocumentTemplate.Compile(NoteLetters.Letter(body, [], ["Membership runs from April to March."]), NoFields, [new HeliographPdfConverter()]).Diagnostics
            .Should().Contain(d => d.Code == DiagnosticCodes.PdfCannotLayOutNotes && d.Message.Contains("differently", StringComparison.Ordinal));
    }

    private static MemoryStream Letter(string kind)
    {
        var body = new List<string> { NoteLetters.EndReferring("Your membership renews in April.", 1) };
        var plain = kind switch { "endnotes on a later page" => 40, "endnotes running over a page" => 33, _ => 2 };
        for (var i = 0; i < plain; i++)
        {
            body.Add(NoteLetters.Plain($"Paragraph {i} of the body of the letter, about the renewal of your membership."));
        }

        body.Add(kind.EndsWith("with footnotes", StringComparison.Ordinal) ? NoteLetters.Referring("The late charge is ten pounds.", 1) : NoteLetters.EndReferring("The late charge is ten pounds.", 2));
        body.Add(NoteLetters.Plain("We look forward to seeing you."));
        var endnotes = kind == "endnotes running over a page" ? Enumerable.Repeat(Long + " " + Long, 6).ToArray() : ["Membership runs from April to March.", Long];
        var footnotes = kind.EndsWith("with footnotes", StringComparison.Ordinal) ? new[] { "Ten pounds for each month late." } : [];
        // Word writes the numbering into the section's settings when it is set in its dialog.
        if (kind.StartsWith("numbered as the settings say", StringComparison.Ordinal))
        {
            body.Add("<w:sectPr xmlns:w=\"http://schemas.openxmlformats.org/wordprocessingml/2006/main\"><w:footnotePr><w:numFmt w:val=\"lowerLetter\"/></w:footnotePr>"
                + "<w:endnotePr><w:numFmt w:val=\"decimal\"/><w:numStart w:val=\"5\"/></w:endnotePr></w:sectPr>");
        }

        return NoteLetters.Letter(body, footnotes, endnotes);
    }

    private static byte[] Rendered(MemoryStream template)
    {
        var compilation = DocumentTemplate.Compile(template, NoFields, [new HeliographPdfConverter()]);
        compilation.Diagnostics.Should().BeEmpty();
        return compilation.Document!.Render(JsonDocument.Parse("{}").RootElement, Benchmark.British).Docx!;
    }
}
