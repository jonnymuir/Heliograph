using System.Reflection;
using ModelContextProtocol.Server;

namespace Heliograph.Mcp;

/// <summary>One guide from docs/skills: the file a person reads, served unchanged to an agent.</summary>
/// <param name="Name">The skill's name, which is its folder's name.</param>
/// <param name="Description">What it covers and when to read it, from the file's own front matter.</param>
/// <param name="Text">The whole file.</param>
public sealed record Skill(string Name, string Description, string Text)
{
    /// <summary>The address an agent reads the skill at.</summary>
    public string Uri => $"{Skills.Scheme}://{Name}";
}

/// <summary>
/// The guides in docs/skills, embedded when this package is built. Each is an MCP resource whose name and
/// description come from the file's front matter, so adding a guide adds a resource and nothing is written twice.
/// </summary>
public static class Skills
{
    /// <summary>The scheme of a skill's address: <c>heliograph-skills://writing-a-template</c>.</summary>
    public const string Scheme = "heliograph-skills";

    private const string Prefix = "Heliograph.Mcp.skills.";

    /// <summary>Every skill, in order of name.</summary>
    public static IReadOnlyList<Skill> All { get; } = Load();

    internal static IEnumerable<McpServerResource> Resources() => All.Select(skill => McpServerResource.Create(
        () => skill.Text,
        new McpServerResourceCreateOptions
        {
            UriTemplate = skill.Uri,
            Name = skill.Name,
            Title = Title(skill.Text) ?? skill.Name,
            Description = skill.Description,
            MimeType = "text/markdown",
        }));

    private static List<Skill> Load()
    {
        var assembly = typeof(Skills).Assembly;
        return [.. assembly.GetManifestResourceNames()
            .Where(name => name.StartsWith(Prefix, StringComparison.Ordinal))
            .Select(name => Read(assembly, name))
            .OrderBy(skill => skill.Name, StringComparer.Ordinal)];
    }

    private static Skill Read(Assembly assembly, string resource)
    {
        using var reader = new StreamReader(assembly.GetManifestResourceStream(resource)!);
        var text = reader.ReadToEnd();
        var folder = resource[Prefix.Length..].Split('/', '\\')[0];
        return new Skill(folder, FrontMatter(text, "description") ?? "", text);
    }

    /// <summary>A value from the block between the two <c>---</c> lines a skill starts with.</summary>
    private static string? FrontMatter(string text, string key)
    {
        var lines = text.ReplaceLineEndings("\n").Split('\n');
        if (lines[0] != "---")
        {
            return null;
        }

        return lines.Skip(1).TakeWhile(line => line != "---")
            .Where(line => line.StartsWith(key + ":", StringComparison.Ordinal))
            .Select(line => line[(key.Length + 1)..].Trim())
            .FirstOrDefault();
    }

    private static string? Title(string text) => text.ReplaceLineEndings("\n").Split('\n')
        .Where(line => line.StartsWith("# ", StringComparison.Ordinal)).Select(line => line[2..].Trim()).FirstOrDefault();
}
