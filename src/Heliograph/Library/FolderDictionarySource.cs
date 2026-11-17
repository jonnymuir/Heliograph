using System.Text.RegularExpressions;

namespace Heliograph.Library;

/// <summary>
/// Dictionaries kept as files in a folder: <c>&lt;name&gt;.json</c> for each dictionary, and
/// <c>&lt;name&gt;.samples/&lt;sample&gt;.json</c> for its sample recipients. The smallest dictionary source a host can
/// use, and the one the reference app uses.
/// </summary>
public sealed partial class FolderDictionarySource(string folder) : IDictionarySource
{
    private const string SampleSuffix = ".samples";

    private readonly string _root = Path.GetFullPath(folder);

    public Task<IReadOnlyList<string>> ListAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(NamesIn(_root));

    public async Task<string?> ReadAsync(string name, CancellationToken cancellationToken = default) =>
        Inside(name + ".json") is { } path && File.Exists(path)
            ? await File.ReadAllTextAsync(path, cancellationToken).ConfigureAwait(false)
            : null;

    public Task<IReadOnlyList<string>> ListSamplesAsync(string name, CancellationToken cancellationToken = default) =>
        Task.FromResult(Inside(name + SampleSuffix) is { } samples ? NamesIn(samples) : []);

    public async Task<string?> ReadSampleAsync(string name, string sample, CancellationToken cancellationToken = default)
    {
        if (!IsName(sample) || Inside(name + SampleSuffix) is not { } samples)
        {
            return null;
        }

        var path = Path.GetFullPath(Path.Combine(samples, sample + ".json"));
        return path.StartsWith(samples + Path.DirectorySeparatorChar, StringComparison.Ordinal) && File.Exists(path)
            ? await File.ReadAllTextAsync(path, cancellationToken).ConfigureAwait(false)
            : null;
    }

    private static IReadOnlyList<string> NamesIn(string directory) => Directory.Exists(directory)
        ? [.. Directory.GetFiles(directory, "*.json").Select(Path.GetFileNameWithoutExtension).OfType<string>().Where(IsName).Order(StringComparer.Ordinal)]
        : [];

    /// <summary>
    /// The full path of an entry directly in the folder, or null. A name becomes part of a path, so it must be a plain
    /// name, and the path it makes must still be inside the folder.
    /// </summary>
    private string? Inside(string entry)
    {
        var name = entry.EndsWith(".json", StringComparison.Ordinal) ? entry[..^5] : entry.EndsWith(SampleSuffix, StringComparison.Ordinal) ? entry[..^SampleSuffix.Length] : entry;
        if (!IsName(name))
        {
            return null;
        }

        var path = Path.GetFullPath(Path.Combine(_root, entry));
        return path.StartsWith(_root + Path.DirectorySeparatorChar, StringComparison.Ordinal) ? path : null;
    }

    private static bool IsName(string? name) => name is not null && NamePattern().IsMatch(name);

    [GeneratedRegex("^[A-Za-z0-9][A-Za-z0-9-]*$")]
    private static partial Regex NamePattern();
}
