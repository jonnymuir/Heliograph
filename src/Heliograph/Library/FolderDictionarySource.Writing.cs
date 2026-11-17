namespace Heliograph.Library;

/// <summary>Writing dictionaries and sample recipients to the folder. Every path is checked to stay inside it.</summary>
public sealed partial class FolderDictionarySource : IDictionaryStore
{
    public async Task SaveAsync(string name, string json, CancellationToken cancellationToken = default)
    {
        var root = Path.GetFullPath(folder);
        var path = Path.GetFullPath(Path.Combine(root, name + ".json"));
        if (!IsName(name) || !path.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.Ordinal))
        {
            throw new ArgumentException($"\"{name}\" cannot be used as a dictionary's name.", nameof(name));
        }

        Directory.CreateDirectory(root);
        await File.WriteAllTextAsync(path, json, cancellationToken).ConfigureAwait(false);
    }

    public async Task SaveSampleAsync(string name, string sample, string json, CancellationToken cancellationToken = default)
    {
        var root = Path.GetFullPath(folder);
        var path = Path.GetFullPath(Path.Combine(root, name + SampleSuffix, sample + ".json"));
        if (!IsName(name) || !IsName(sample) || !path.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.Ordinal))
        {
            throw new ArgumentException($"\"{name}/{sample}\" cannot be used as a sample's name.", nameof(sample));
        }

        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await File.WriteAllTextAsync(path, json, cancellationToken).ConfigureAwait(false);
    }

    public Task DeleteSampleAsync(string name, string sample, CancellationToken cancellationToken = default)
    {
        // Found among the files the folder lists, so a name is never made into a path.
        if (SamplesFolder(name) is { } samples && Entries(samples).TryGetValue(sample, out var path))
        {
            File.Delete(path);
        }

        return Task.CompletedTask;
    }
}
