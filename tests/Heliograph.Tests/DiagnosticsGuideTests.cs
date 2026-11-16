using System.Reflection;

namespace Heliograph.Tests;

/// <summary>The guide to diagnostics is where a person or an agent looks a code up, so it must list every one.</summary>
public class DiagnosticsGuideTests
{
    [Fact]
    public void Every_diagnostic_code_is_explained_in_the_guide()
    {
        var guide = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "docs", "skills", "diagnostics", "SKILL.md"));
        var codes = typeof(DiagnosticCodes).GetFields(BindingFlags.Public | BindingFlags.Static)
            .Select(field => (string)field.GetRawConstantValue()!).ToList();

        codes.Should().NotBeEmpty();
        codes.Where(code => !guide.Contains($"`{code}`", StringComparison.Ordinal)).Should().BeEmpty();
    }
}
