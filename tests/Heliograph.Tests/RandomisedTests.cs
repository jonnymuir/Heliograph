using Heliograph.Documents;
using Heliograph.Tests.Support;

namespace Heliograph.Tests;

/// <summary>
/// Tests that do not depend on anyone thinking of the case. Each takes a seed, so a failure names the document
/// that caused it and can be run again.
/// </summary>
public class RandomisedTests
{
    private const int Templates = 400;
    private const int Choppings = 25;

    public static TheoryData<string, string> BenchmarkSamples() => new()
    {
        { "acknowledgement", "awkward-characters" },
        { "decision-letter", "approved-dangerous-props" },
        { "decision-letter", "refused" },
    };

    [Theory]
    [MemberData(nameof(BenchmarkSamples))]
    public void However_an_editor_chops_the_text_into_pieces_the_letter_reads_the_same(string letter, string sample)
    {
        var template = File.ReadAllBytes(Benchmark.PathTo(letter, "document.docx"));
        var expected = Paragraphs(template, letter, sample);

        for (var seed = 1; seed <= Choppings; seed++)
        {
            Paragraphs(RandomTemplate.Chopped(template, seed), letter, sample).Should().Equal(expected, $"chopping seed {seed}");
        }
    }

    [Fact]
    public void A_random_template_is_either_refused_with_reasons_or_renders_a_valid_document_for_every_recipient()
    {
        var compiled = 0;
        for (var seed = 1; seed <= Templates; seed++)
        {
            var template = RandomTemplate.Build(seed).Build().ToArray();
            var compilation = DocumentTemplate.Compile(new MemoryStream(template), RandomTemplate.Dictionary);
            if (!compilation.Succeeded)
            {
                compilation.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).Should().NotBeEmpty($"seed {seed} was refused");
                compilation.Diagnostics.Should().OnlyContain(d => d.Code.Length == 6 && d.Message.Length > 0 && d.Fix.Length > 0, $"seed {seed}");
                continue;
            }

            compiled++;
            var before = WordSchema.Problems(template);
            for (var answers = 0; answers < 8; answers++)
            {
                var render = compilation.Document!.Render(Benchmark.Data(RandomTemplate.Data(answers)), Benchmark.British);
                var where = $"seed {seed}, answers {answers}";

                render.Diagnostics.Should().BeEmpty(where);
                WordSchema.Problems(render.Docx!).Except(before).Should().BeEmpty(where);
                var xml = TestDocx.PartText(render.Docx!, "word/document.xml");
                xml.Should().NotContain("urn:heliograph", where);
                xml.Should().NotContain("((", where);
            }
        }

        compiled.Should().BeGreaterThan(Templates / 10, "enough random templates must compile for the render checks to mean something");
    }

    private static IReadOnlyList<string> Paragraphs(byte[] template, string letter, string sample)
    {
        var compilation = DocumentTemplate.Compile(new MemoryStream(template), Benchmark.Dictionary(letter));
        compilation.Diagnostics.Should().BeEmpty();
        return TestDocx.Paragraphs(compilation.Document!.Render(Benchmark.Sample(letter, sample), Benchmark.British).Docx!);
    }
}
