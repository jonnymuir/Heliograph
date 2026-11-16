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
}
