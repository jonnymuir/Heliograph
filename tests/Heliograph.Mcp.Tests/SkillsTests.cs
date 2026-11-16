using ModelContextProtocol.Protocol;

namespace Heliograph.Mcp.Tests;

/// <summary>
/// A guide in docs/skills is written once. A person reads it on GitHub, an agent's tooling loads it as a skill, and
/// an agent connected over MCP reads it as a resource: the same file each time.
/// </summary>
public class SkillsTests
{
    private static readonly string SkillsFolder = Path.Combine(AppContext.BaseDirectory, "docs", "skills");

    private static List<string> Names => [.. Directory.GetDirectories(SkillsFolder).Select(folder => Path.GetFileName(folder)!)];

    public static TheoryData<string> Folders() => [.. Names];

    [Fact]
    public async Task Every_guide_in_the_skills_folder_is_a_resource_an_agent_can_read_word_for_word()
    {
        await using var host = await AuthoringHost.StartAsync();
        await using var agent = await host.ConnectAsync();

        var resources = await agent.ListResourcesAsync();

        resources.Select(resource => resource.Uri).Should().BeEquivalentTo(Names.Select(folder => $"heliograph-skills://{folder}"));
        foreach (var resource in resources)
        {
            var read = await agent.ReadResourceAsync(resource.Uri);
            var onDisk = File.ReadAllText(Path.Combine(SkillsFolder, resource.Name, "SKILL.md"));
            read.Contents.OfType<TextResourceContents>().Should().ContainSingle().Which.Text.Should().Be(onDisk);
            resource.MimeType.Should().Be("text/markdown");
        }
    }

    [Theory]
    [MemberData(nameof(Folders))]
    public void A_skill_starts_with_front_matter_that_names_it_after_its_folder_and_says_when_to_use_it(string folder)
    {
        var lines = File.ReadAllLines(Path.Combine(SkillsFolder, folder, "SKILL.md"));
        var frontMatter = lines.Skip(1).TakeWhile(line => line != "---").ToList();

        lines[0].Should().Be("---");
        frontMatter.Should().Contain($"name: {folder}");
        var description = frontMatter.Should().ContainSingle(line => line.StartsWith("description: ", StringComparison.Ordinal)).Subject["description: ".Length..];
        description.Should().Contain("Use ", "a skill's description says when to reach for it");
        description.Should().NotContain(": ", "a colon followed by a space ends a YAML value, and the rest would be lost");
        description.Length.Should().BeLessThanOrEqualTo(1024);
    }

    [Theory]
    [MemberData(nameof(Folders))]
    public void What_an_agent_is_told_about_a_skill_is_what_its_front_matter_says(string folder)
    {
        var skill = Skills.All.Should().ContainSingle(skill => skill.Name == folder).Subject;

        File.ReadAllText(Path.Combine(SkillsFolder, folder, "SKILL.md")).Should().Contain($"description: {skill.Description}\n");
        skill.Description.Should().NotBeEmpty();
    }
}
