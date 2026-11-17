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

    [Fact]
    public void The_fonts_that_ship_are_listed_with_their_licence_and_the_licence_ships_with_them()
    {
        var listed = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "docs", "third-party.md"));
        var package = Projects().Single(p => p.Descendants("PackageId").Any(id => id.Value == "Heliograph.Pdf"));

        listed.Should().Contain("## Fonts that ship").And.Contain("Liberation Sans, Serif and Mono").And.Contain("SIL Open Font License");
        package.Descendants("None").Select(n => n.Attribute("Include")?.Value).Should().Contain("Fonts/BuiltIn/LICENSE-Liberation.txt");
    }

    private static List<XDocument> Projects() =>
        Directory.GetFiles(Shipped, "*.csproj", SearchOption.AllDirectories).Select(XDocument.Load).ToList();
}
