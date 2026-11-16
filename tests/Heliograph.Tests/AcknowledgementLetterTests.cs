using System.IO.Compression;
using Heliograph.Tests.Support;

namespace Heliograph.Tests;

/// <summary>The first benchmark letter: values only. See benchmarks/acknowledgement.</summary>
public class AcknowledgementLetterTests
{
    private const string Letter = "acknowledgement";

    [Fact]
    public void The_template_compiles_with_nothing_to_report()
    {
        var compilation = Benchmark.Compile(Letter);

        compilation.Diagnostics.Should().BeEmpty();
        compilation.Succeeded.Should().BeTrue();
    }

    [Fact]
    public void Every_field_is_filled_in_from_the_data()
    {
        var paragraphs = RenderParagraphs("typical");

        paragraphs.Should().Contain("Dear Alex Applicant,");
        paragraphs.Should().Contain("Our reference: NJF-2026-004217");
        paragraphs.Should().Contain(
            "We received your application for Clubtown Summer Juggle, taking place on 19 June 2027, on 16 November 2026.");
        string.Concat(paragraphs).Should().NotContain("((");
    }

    [Fact]
    public void A_date_is_written_out_in_words_without_the_author_choosing_a_format()
    {
        RenderParagraphs("typical").Should().Contain("17 November 2026");
    }

    [Fact]
    public void An_address_is_printed_one_line_under_another()
    {
        RenderParagraphs("typical").Should().Contain("12 Tumble Lane\nClubtown\nZZ1 1ZZ");
    }

    [Fact]
    public void A_field_in_the_footer_is_filled_in_too()
    {
        var render = Benchmark.Compile(Letter).Document!.Render(Benchmark.Sample(Letter, "typical"), Benchmark.British);

        string.Concat(TestDocx.HeaderAndFooterParagraphs(render.Docx!)).Should().Contain("Reference NJF-2026-004217");
    }

    [Fact]
    public void Awkward_characters_are_printed_exactly_as_supplied()
    {
        var paragraphs = RenderParagraphs("awkward-characters");

        paragraphs.Should().Contain("Dear Siobhán O'Hare-Ng,");
        paragraphs.Should().Contain(
            "Flat 3, The Old Circus & Fairground\n9 <Upper> Catch Street\nLittle Throwing-on-the-Wold\nClubshire\nZZ9 9ZZ");
        string.Concat(paragraphs).Should().Contain(
            "The Little Throwing-on-the-Wold \"Fire & Knives\" Midsummer Spectacular <2027>");
    }

    [Fact]
    public void The_same_template_and_data_give_the_same_file_byte_for_byte()
    {
        var document = Benchmark.Compile(Letter).Document!;
        var data = Benchmark.Sample(Letter, "typical");

        var first = document.Render(data, Benchmark.British).Docx!;
        var second = Benchmark.Compile(Letter).Document!.Render(data, Benchmark.British).Docx!;

        second.Should().Equal(first);
    }

    [Fact]
    public void A_rendered_file_does_not_record_when_it_was_made()
    {
        var docx = Benchmark.Compile(Letter).Document!.Render(Benchmark.Sample(Letter, "typical"), Benchmark.British).Docx!;

        using var zip = new ZipArchive(new MemoryStream(docx), ZipArchiveMode.Read);
        zip.Entries.Select(entry => entry.LastWriteTime.Year).Distinct().Should().Equal(1980);
    }

    [Fact]
    public void The_copy_saved_by_Word_in_Microsoft_365_compiles_and_renders()
    {
        var compilation = Benchmark.Compile(Letter, "document.word-365.docx");

        // It was saved with no title, which is pointed out and stops nothing.
        compilation.Diagnostics.Should().ContainSingle().Which.Code.Should().Be(DiagnosticCodes.DocumentHasNoTitle);
        var render = compilation.Document!.Render(Benchmark.Sample(Letter, "typical"), Benchmark.British);
        var paragraphs = TestDocx.Paragraphs(render.Docx!);
        paragraphs.Should().Contain("Hi Alex Applicant,");
        paragraphs.Should().Contain("12 Tumble Lane\nClubtown\nZZ1 1ZZ");
        string.Concat(paragraphs).Should().NotContain("((");
    }

    [Fact]
    public void A_letter_does_not_tell_its_recipient_who_edited_the_template()
    {
        var render = Benchmark.Compile(Letter, "document.word-365.docx").Document!
            .Render(Benchmark.Sample(Letter, "typical"), Benchmark.British);

        TestDocx.Property(render.Docx!, "lastModifiedBy").Should().BeEmpty();
        TestDocx.Property(render.Docx!, "creator").Should().BeEmpty();
    }

    [Fact]
    public void Rendering_one_letter_does_not_change_the_next()
    {
        var document = Benchmark.Compile(Letter).Document!;

        document.Render(Benchmark.Sample(Letter, "awkward-characters"), Benchmark.British);
        var paragraphs = TestDocx.Paragraphs(document.Render(Benchmark.Sample(Letter, "typical"), Benchmark.British).Docx!);

        paragraphs.Should().Contain("Dear Alex Applicant,");
    }

    private static IReadOnlyList<string> RenderParagraphs(string sample)
    {
        var render = Benchmark.Compile(Letter).Document!.Render(Benchmark.Sample(Letter, sample), Benchmark.British);
        render.Diagnostics.Should().BeEmpty();
        return TestDocx.Paragraphs(render.Docx!);
    }
}
