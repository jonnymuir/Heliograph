using Heliograph.Documents;
using Heliograph.Tests.Support;

namespace Heliograph.Tests;

/// <summary>A template must mean the same after an editor has opened and saved it.</summary>
public class EditorRoundTripTests
{
    [EditorFact]
    public void The_acknowledgement_renders_the_same_after_LibreOffice_has_saved_it()
    {
        const string letter = "acknowledgement";
        var data = Benchmark.Sample(letter, "awkward-characters");
        var before = Benchmark.Compile(letter).Document!.Render(data, Benchmark.British).Docx!;

        using var saved = new MemoryStream(LibreOffice.OpenAndSave(Benchmark.PathTo(letter, "document.docx")));
        var compilation = DocumentTemplate.Compile(saved, Benchmark.Dictionary(letter));

        compilation.Diagnostics.Should().BeEmpty();
        var after = compilation.Document!.Render(data, Benchmark.British).Docx!;
        TestDocx.Paragraphs(after).Should().Equal(TestDocx.Paragraphs(before));
        TestDocx.HeaderAndFooterParagraphs(after).Where(text => text.Length > 0)
            .Should().Contain(TestDocx.HeaderAndFooterParagraphs(before).Where(text => text.Length > 0));
    }

    [EditorFact]
    public void A_letter_with_a_table_cell_emptied_by_a_condition_still_opens_in_LibreOffice()
    {
        var dictionary = new FieldDictionary([new Field("Owes money", "owesMoney", FieldType.YesNo)]);
        using var stream = new TestDocx().RawParagraph(
            "<w:tbl><w:tr><w:tc><w:p><w:r><w:t>((if Owes money))</w:t></w:r></w:p><w:p><w:r><w:t>You owe us.</w:t></w:r></w:p>"
            + "<w:p><w:r><w:t>((end if))</w:t></w:r></w:p></w:tc><w:tc><w:p><w:r><w:t>Kept.</w:t></w:r></w:p></w:tc></w:tr></w:tbl>")
            .Paragraph("After.").Build();
        var rendered = DocumentTemplate.Compile(stream, dictionary).Document!
            .Render(Benchmark.Data("""{ "owesMoney": false }"""), Benchmark.British).Docx!;

        var reopened = LibreOffice.OpenAndSave(rendered);

        TestDocx.Paragraphs(reopened).Should().Contain("Kept.").And.Contain("After.").And.NotContain("You owe us.");
    }
}
