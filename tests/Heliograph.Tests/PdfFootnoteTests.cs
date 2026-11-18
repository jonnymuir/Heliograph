using System.Text;
using System.Text.Json;
using Heliograph.Documents;
using Heliograph.Pdf;
using Heliograph.Pdf.LibreOffice;
using Heliograph.Tests.Support;

namespace Heliograph.Tests;

/// <summary>
/// Footnotes in Heliograph's own PDF converter: numbered in order, at the foot of the page under the separator, the body
/// stopping above them, and a line that refers to a note moving to the next page with it, laid out as LibreOffice does.
/// </summary>
public class PdfFootnoteTests
{
    private const string W = "xmlns:w=\"http://schemas.openxmlformats.org/wordprocessingml/2006/main\"";
    private const string Long = "Charged on fees paid after the end of April, which is a long note that runs on to a second line of note text.";
    private static readonly FieldDictionary NoFields = new([]);

    public static TheoryData<string> Letters => ["two notes", "a note pushing its line over", "notes on two pages", "a long note"];

    [ComparedPdfTheory]
    [MemberData(nameof(Letters))]
    public async Task Footnotes_are_laid_out_as_LibreOffice_lays_them_out(string kind)
    {
        var docx = Rendered(Letter(kind));
        var expected = PdfLines.Read(await new LibreOfficePdfConverter().ConvertAsync(docx));
        var actual = PdfLines.Read(await new HeliographPdfConverter().ConvertAsync(docx));

        PdfComparison.FirstDifference(expected, actual, "LibreOffice").Difference.Should().BeNull(kind);
    }

    [PdfLinesFact]
    public async Task Notes_are_numbered_in_the_order_they_are_referred_to_and_drawn_under_the_separator()
    {
        var pdf = await new HeliographPdfConverter().ConvertAsync(Rendered(Letter("two notes")));
        var lines = PdfLines.Read(pdf);
        var note = lines.Single(l => l.Text.StartsWith("1 Membership", StringComparison.Ordinal));

        lines.Should().Contain(l => l.Text.StartsWith("2 Charged", StringComparison.Ordinal));
        lines.Should().Contain(l => l.Text.EndsWith("April. 1", StringComparison.Ordinal));
        var page = PdfRender.FirstPage(pdf);
        Enumerable.Range(2, 8).Select(dy => page.At(100, note.Top - dy)).Should().Contain(c => c.R < 100, "the separator is drawn above the notes");
    }

    [VeraPdfTheory]
    [InlineData("two notes")]
    [InlineData("notes on two pages")]
    public async Task A_letter_with_footnotes_is_still_an_accessible_PDF(string kind)
    {
        var pdf = await new HeliographPdfConverter().ConvertAsync(Rendered(Letter(kind)));

        Encoding.Latin1.GetString(pdf).Should().Contain("/S /Note").And.Contain("/ID (note-1)");
        PdfValidator.FailuresAsPdfUa(pdf).Should().BeEmpty();
    }

    [Theory]
    [InlineData("<w:footnoteReference w:id=\"1\"/>", "<w:endnoteReference w:id=\"1\"/>", "an endnote")]
    [InlineData("<w:footnoteReference w:id=\"1\"/>", "<w:footnoteReference w:customMarkFollows=\"1\" w:id=\"1\"/><w:t>*</w:t>", "a mark of its own")]
    public void A_note_it_cannot_lay_out_is_refused_at_compile(string from, string to, string named)
    {
        var docx = NoteLetters.Letter([NoteLetters.Referring("Your membership renews in April.", 1).Replace(from, to, StringComparison.Ordinal)], "Membership runs from April to March.");

        DocumentTemplate.Compile(docx, NoFields, [new HeliographPdfConverter()]).Diagnostics
            .Should().ContainSingle(d => d.Code == DiagnosticCodes.PdfCannotLayOutNotes).Which.Message.Should().Contain(named);
    }

    [Fact]
    public void A_footnote_in_a_table_is_refused_at_compile()
    {
        var table = $"<w:tbl {W}><w:tblGrid><w:gridCol w:w=\"4000\"/></w:tblGrid><w:tr><w:tc>{NoteLetters.Referring("In a cell.", 1).Replace($" {W}", "", StringComparison.Ordinal)}</w:tc></w:tr></w:tbl>";

        DocumentTemplate.Compile(NoteLetters.Letter([table], "A note."), NoFields, [new HeliographPdfConverter()]).Diagnostics
            .Should().ContainSingle(d => d.Code == DiagnosticCodes.PdfCannotLayOutNotes).Which.Message.Should().Contain("in a table");
    }

    [Fact]
    public void An_ordinary_footnote_is_not_refused()
    {
        DocumentTemplate.Compile(Letter("two notes"), NoFields, [new HeliographPdfConverter()]).Diagnostics.Should().BeEmpty();
    }

    private static MemoryStream Letter(string kind)
    {
        var body = new List<string>();
        var plain = kind switch { "a note pushing its line over" => 25, "notes on two pages" => 40, _ => 2 };
        for (var i = 0; i < plain; i++)
        {
            body.Add(NoteLetters.Plain($"Paragraph {i} of the body of the letter, about the renewal of your membership."));
        }

        body.Insert(0, NoteLetters.Referring("Your membership renews in April.", 1));
        body.Insert(Math.Min(body.Count, kind == "notes on two pages" ? 32 : body.Count), NoteLetters.Referring("The late charge is ten pounds.", 2));
        if (kind == "a note pushing its line over")
        {
            body.Add(NoteLetters.Referring("And a third note, at the foot of the page.", 3));
            body.Add(NoteLetters.Plain("We look forward to seeing you."));
        }

        return NoteLetters.Letter(body, "Membership runs from April to March.", kind == "a long note" ? Long + " " + Long : Long, "A third note.");
    }

    private static byte[] Rendered(MemoryStream template)
    {
        var compilation = DocumentTemplate.Compile(template, NoFields, [new HeliographPdfConverter()]);
        compilation.Diagnostics.Should().BeEmpty();
        return compilation.Document!.Render(JsonDocument.Parse("{}").RootElement, Benchmark.British).Docx!;
    }
}
