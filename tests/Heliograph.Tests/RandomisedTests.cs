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
    public void A_random_template_with_a_mistake_in_it_is_refused_with_reasons()
    {
        var checkedTemplates = 0;
        for (var seed = 1; seed <= Templates; seed++)
        {
            var template = new RandomTemplate(seed);
            if (!template.HasMistake)
            {
                continue;
            }

            checkedTemplates++;
            var compilation = DocumentTemplate.Compile(new MemoryStream(template.Docx()), RandomTemplate.Dictionary);

            compilation.Succeeded.Should().BeFalse($"seed {seed} has a condition tag with no partner");
            compilation.Diagnostics.Should().OnlyContain(d => d.Code.Length == 6 && d.Message.Length > 0 && d.Fix.Length > 0, $"seed {seed}");
        }

        checkedTemplates.Should().BeGreaterThan(Templates / 10);
    }

    [Fact]
    public void A_random_template_with_no_mistake_says_exactly_what_it_should_to_every_recipient_in_a_valid_file()
    {
        var checkedTemplates = 0;
        for (var seed = 1; seed <= Templates; seed++)
        {
            var template = new RandomTemplate(seed);
            if (template.HasMistake)
            {
                continue;
            }

            checkedTemplates++;
            var docx = template.Docx();
            var compilation = DocumentTemplate.Compile(new MemoryStream(docx), RandomTemplate.Dictionary);
            compilation.Diagnostics.Should().BeEmpty($"seed {seed} has no mistake in it");
            var schemaBefore = WordSchema.Problems(docx);

            for (var answers = 0; answers < 8; answers++)
            {
                var where = $"seed {seed}, answers {answers}";
                var render = compilation.Document!.Render(Benchmark.Data(RandomTemplate.Data(answers)), Benchmark.British);

                render.Diagnostics.Should().BeEmpty(where);
                TestDocx.Paragraphs(render.Docx!).Should().Equal(template.Expected(answers), where);
                WordRules.Problems(render.Docx!).Should().BeEmpty(where);
                WordSchema.Problems(render.Docx!).Except(schemaBefore).Should().BeEmpty(where);
            }
        }

        checkedTemplates.Should().BeGreaterThan(Templates / 3);
    }

    private static IReadOnlyList<string> Paragraphs(byte[] template, string letter, string sample)
    {
        var compilation = DocumentTemplate.Compile(new MemoryStream(template), Benchmark.Dictionary(letter));
        compilation.Diagnostics.Should().BeEmpty();
        return TestDocx.Paragraphs(compilation.Document!.Render(Benchmark.Sample(letter, sample), Benchmark.British).Docx!);
    }
}
