using System.Xml.Linq;

namespace Heliograph.Tests;

/// <summary>What ships depends only on software whose licence has been written down.</summary>
public class ThirdPartyTests
{
    private static readonly string Shipped = Path.Combine(AppContext.BaseDirectory, "shipped");

    [Fact]
    public void Every_package_a_shipped_project_references_is_listed_with_its_licence()
    {
        var listed = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "docs", "third-party.md"));
        var whatShips = listed[listed.IndexOf("## What ships", StringComparison.Ordinal)..listed.IndexOf("## What is run", StringComparison.Ordinal)];

        var referenced = Projects().SelectMany(project => project.Descendants("PackageReference"))
            .Select(reference => reference.Attribute("Include")!.Value).Distinct();

        referenced.Should().OnlyContain(package => whatShips.Contains($"| `{package}` |", StringComparison.Ordinal),
            "a package that ships with Heliograph needs a row in docs/third-party.md saying what its licence is");
    }

    [Fact]
    public void No_shipped_project_refers_to_the_pdf_validator_the_tests_run()
    {
        Projects().Should().NotBeEmpty();
        Projects().Select(project => project.ToString()).Should().OnlyContain(project => !project.Contains("verapdf", StringComparison.OrdinalIgnoreCase));
    }

    private static List<XDocument> Projects() =>
        Directory.GetFiles(Shipped, "*.csproj", SearchOption.AllDirectories).Select(XDocument.Load).ToList();
}
