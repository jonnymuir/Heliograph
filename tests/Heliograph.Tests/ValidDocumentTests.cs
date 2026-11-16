using Heliograph.Tests.Support;

namespace Heliograph.Tests;

/// <summary>
/// A rendered file must be a valid Word document for every recipient. These tests ask an independent validator,
/// not the engine. An editor's own file may already bend the schema, so the rule is that a render adds no problem
/// the template did not have.
/// </summary>
public class ValidDocumentTests
{
    public static TheoryData<string, string, string> EverySample()
    {
        var data = new TheoryData<string, string, string>();
        var benchmarks = Path.Combine(AppContext.BaseDirectory, "benchmarks");
        foreach (var letter in Directory.GetDirectories(benchmarks).Select(Path.GetFileName).Order(StringComparer.Ordinal))
        {
            foreach (var template in Directory.GetFiles(Path.Combine(benchmarks, letter!), "*.docx").Select(Path.GetFileName).Order(StringComparer.Ordinal))
            {
                foreach (var sample in Directory.GetFiles(Path.Combine(benchmarks, letter!, "samples"), "*.json").Order(StringComparer.Ordinal))
                {
                    data.Add(letter!, template!, Path.GetFileNameWithoutExtension(sample));
                }
            }
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(EverySample))]
    public void Rendering_a_benchmark_letter_adds_no_schema_problem_the_template_did_not_have(string letter, string template, string sample)
    {
        var before = WordSchema.Problems(File.ReadAllBytes(Benchmark.PathTo(letter, template)));
        var render = Benchmark.Compile(letter, template).Document!.Render(Benchmark.Sample(letter, sample), Benchmark.British);
        render.Diagnostics.Should().BeEmpty();

        WordSchema.Problems(render.Docx!).Except(before).Should().BeEmpty();
        WordRules.Problems(render.Docx!).Should().BeEmpty();
    }

    [Fact]
    public void There_are_benchmark_letters_to_check()
    {
        EverySample().Should().HaveCountGreaterThanOrEqualTo(7);
    }
}
