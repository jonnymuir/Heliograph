using System.Text.RegularExpressions;

namespace Heliograph.Library;

/// <summary>
/// Dictionaries kept as files in a folder: <c>&lt;name&gt;.json</c> for each dictionary, and
/// <c>&lt;name&gt;.samples/&lt;sample&gt;.json</c> for its sample recipients. The smallest dictionary source a host can
/// use, and the one the reference app uses.
/// </summary>
/// <remarks>
/// A name asked for is never made into a path. It is looked up among the files the folder lists, and only a path the
/// folder itself gave is opened, so nothing outside the folder can be reached.
/// </remarks>
public sealed partial class FolderDictionarySource(string folder) : IDictionarySource
{
    private const string SampleSuffix = ".samples";

    public Task<IReadOnlyList<string>> ListAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<string>>([.. Entries(folder).Keys.Order(StringComparer.Ordinal)]);

    public async Task<string?> ReadAsync(string name, CancellationToken cancellationToken = default) =>
        Entries(folder).TryGetValue(name, out var path)
            ? await File.ReadAllTextAsync(path, cancellationToken).ConfigureAwait(false)
            : null;

    public Task<IReadOnlyList<string>> ListSamplesAsync(string name, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<string>>(SamplesFolder(name) is { } samples ? [.. Entries(samples).Keys.Order(StringComparer.Ordinal)] : []);

    public async Task<string?> ReadSampleAsync(string name, string sample, CancellationToken cancellationToken = default) =>
        SamplesFolder(name) is { } samples && Entries(samples).TryGetValue(sample, out var path)
            ? await File.ReadAllTextAsync(path, cancellationToken).ConfigureAwait(false)
            : null;

    /// <summary>The sample folder for a dictionary, found among the folders the folder lists.</summary>
    private string? SamplesFolder(string name) => Directory.Exists(folder)
        ? Directory.GetDirectories(folder, "*" + SampleSuffix)
            .FirstOrDefault(path => string.Equals(Path.GetFileName(path), name + SampleSuffix, StringComparison.Ordinal) && IsName(name))
        : null;

    /// <summary>The files a folder holds, by name without the extension. Only plain names are listed.</summary>
    private static Dictionary<string, string> Entries(string directory) => Directory.Exists(directory)
        ? Directory.GetFiles(directory, "*.json")
            .Select(path => (Name: Path.GetFileNameWithoutExtension(path), Path: path))
            .Where(entry => IsName(entry.Name))
            .ToDictionary(entry => entry.Name, entry => entry.Path, StringComparer.Ordinal)
        : [];

    private static bool IsName(string? name) => name is not null && NamePattern().IsMatch(name);

    [GeneratedRegex("^[A-Za-z0-9][A-Za-z0-9-]*$")]
    private static partial Regex NamePattern();
}
