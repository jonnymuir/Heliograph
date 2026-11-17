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

    public Task<IReadOnlyList<string>> ListAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<string>>(Directory.Exists(folder)
            ? [.. Directory.GetFiles(folder, "*.json").Select(Path.GetFileNameWithoutExtension).OfType<string>().Where(IsName).Order(StringComparer.Ordinal)]
            : []);

    public async Task<string?> ReadAsync(string name, CancellationToken cancellationToken = default) =>
        IsName(name) && File.Exists(Path.Combine(folder, name + ".json"))
            ? await File.ReadAllTextAsync(Path.Combine(folder, name + ".json"), cancellationToken).ConfigureAwait(false)
            : null;

    public Task<IReadOnlyList<string>> ListSamplesAsync(string name, CancellationToken cancellationToken = default)
    {
        var samples = Path.Combine(folder, name + SampleSuffix);
        return Task.FromResult<IReadOnlyList<string>>(IsName(name) && Directory.Exists(samples)
            ? [.. Directory.GetFiles(samples, "*.json").Select(Path.GetFileNameWithoutExtension).OfType<string>().Where(IsName).Order(StringComparer.Ordinal)]
            : []);
    }

    public async Task<string?> ReadSampleAsync(string name, string sample, CancellationToken cancellationToken = default)
    {
        var path = Path.Combine(folder, name + SampleSuffix, sample + ".json");
        return IsName(name) && IsName(sample) && File.Exists(path)
            ? await File.ReadAllTextAsync(path, cancellationToken).ConfigureAwait(false)
            : null;
    }

    /// <summary>A name becomes part of a path, so it may hold nothing that leaves the folder.</summary>
    private static bool IsName(string? name) => name is not null && NamePattern().IsMatch(name);

    [GeneratedRegex("^[A-Za-z0-9][A-Za-z0-9-]*$")]
    private static partial Regex NamePattern();
}
