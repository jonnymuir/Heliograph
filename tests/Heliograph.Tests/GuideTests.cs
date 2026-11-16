using Heliograph.Tests.Examples;
using Heliograph.Tests.Support;

namespace Heliograph.Tests;

/// <summary>What the guides teach must be true. Each example in them is run here.</summary>
public class GuideTests
{
    [Fact]
    public void The_code_in_the_README_is_the_code_that_is_compiled_and_tested()
    {
        var readme = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "docs", "README-root.md"));
        var source = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Examples", "Quickstart.cs"));
        var example = Between(source, "// <readme>", "// </readme>").Split('\n')
            .Select(line => line.Length >= 8 ? line[8..] : line.Trim()).ToList();

        Lines(readme).Should().ContainInConsecutiveOrder(Lines(string.Join('\n', example)));
    }

    [Fact]
    public void The_code_in_the_README_renders_a_letter_an_email_and_a_text()
    {
        var output = Directory.CreateTempSubdirectory("heliograph-readme-");
        try
        {
            Quickstart.Run(
                Path.GetDirectoryName(Benchmark.PathTo("acknowledgement", "dictionary.json"))!,
                Benchmark.PathTo("acknowledgement", Path.Combine("samples", "typical.json")),
                output.FullName);

            TestDocx.Paragraphs(File.ReadAllBytes(Path.Combine(output.FullName, "letter.docx"))).Should().Contain("Dear Alex Applicant,");
            File.ReadAllText(Path.Combine(output.FullName, "email.html")).Should().Contain("Dear Alex Applicant,");
            File.ReadAllText(Path.Combine(output.FullName, "sms.txt")).Should().StartWith("NJF: we have your licence application");
        }
        finally
        {
            output.Delete(recursive: true);
        }
    }

    [Theory]
    [InlineData("writing-a-template.md")]
    [InlineData("using-heliograph.md")]
    public void Every_diagnostic_code_a_guide_mentions_is_a_real_one(string guide)
    {
        var text = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "docs", guide));
        var real = typeof(DiagnosticCodes).GetFields().Select(field => (string)field.GetRawConstantValue()!).ToHashSet();

        System.Text.RegularExpressions.Regex.Matches(text, @"HG\d{4}").Select(match => match.Value).Distinct()
            .Should().BeSubsetOf(real);
    }

    private static string Between(string text, string start, string end)
    {
        var from = text.IndexOf(start, StringComparison.Ordinal) + start.Length;
        return text[from..text.IndexOf(end, StringComparison.Ordinal)].Trim('\r', '\n');
    }

    private static List<string> Lines(string text) =>
        [.. text.ReplaceLineEndings("\n").Split('\n').Select(line => line.TrimEnd()).Where(line => line.Length > 0)];
}
