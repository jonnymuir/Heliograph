namespace Heliograph.Library;

/// <summary>
/// The dictionaries a correspondence can use, as an author needs them: the list, each one laid out with its tags,
/// and the sample recipients for previews. Read through the host's <see cref="IDictionarySource"/>.
/// </summary>
public sealed class DictionaryCatalog(IDictionarySource source)
{
    /// <summary>The names of the dictionaries a correspondence can use.</summary>
    public Task<IReadOnlyList<string>> ListAsync(CancellationToken cancellationToken = default) => source.ListAsync(cancellationToken);

    /// <summary>A dictionary as JSON, or null if there is none with this name.</summary>
    public Task<string?> ReadAsync(string name, CancellationToken cancellationToken = default) => source.ReadAsync(name, cancellationToken);

    /// <summary>A dictionary laid out for an author, each field with its tag. Null if there is none, or it cannot be read.</summary>
    public async Task<DictionarySheet?> SheetAsync(string name, CancellationToken cancellationToken = default)
    {
        if (await source.ReadAsync(name, cancellationToken).ConfigureAwait(false) is not { } json)
        {
            return null;
        }

        try
        {
            return DictionarySheet.From(name, FieldDictionary.Parse(json));
        }
        catch (FormatException)
        {
            return null;
        }
    }

    /// <summary>The names of a dictionary's made-up sample recipients.</summary>
    public Task<IReadOnlyList<string>> SamplesAsync(string name, CancellationToken cancellationToken = default) =>
        source.ListSamplesAsync(name, cancellationToken);

    /// <summary>One sample recipient's data as JSON, or null if there is none with this name.</summary>
    public Task<string?> SampleAsync(string name, string sample, CancellationToken cancellationToken = default) =>
        source.ReadSampleAsync(name, sample, cancellationToken);
}
