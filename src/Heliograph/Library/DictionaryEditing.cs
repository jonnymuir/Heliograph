namespace Heliograph.Library;

/// <summary>
/// A dictionary source that can also be written to, so authors can keep dictionaries from the workbench. A host whose
/// dictionaries come from elsewhere (a system that knows its own data) implements only <see cref="IDictionarySource"/>,
/// and the workbench shows them read-only.
/// </summary>
public interface IDictionaryStore : IDictionarySource
{
    /// <summary>Adds or replaces a dictionary. The library has already checked the JSON and the name.</summary>
    Task SaveAsync(string name, string json, CancellationToken cancellationToken = default);

    /// <summary>Adds or replaces one of a dictionary's made-up sample recipients, as a JSON object.</summary>
    Task SaveSampleAsync(string name, string sample, string json, CancellationToken cancellationToken = default);

    /// <summary>Removes a sample recipient. Removing one that is not there does nothing.</summary>
    Task DeleteSampleAsync(string name, string sample, CancellationToken cancellationToken = default);
}

/// <summary>
/// Ready-made dictionaries a host offers as a place to start a new one, such as one for each Wayfinder blueprint, built
/// from its fields. Heliograph knows nothing of where they come from.
/// </summary>
public interface IDictionaryStartingPoints
{
    /// <summary>Every starting point, in the order to show them.</summary>
    Task<IReadOnlyList<DictionaryStartingPoint>> ListAsync(CancellationToken cancellationToken = default);
}

/// <summary>A place to start a new dictionary.</summary>
/// <param name="Name">What it is called, such as "Apply for a juggling event licence".</param>
/// <param name="Description">Where its fields come from.</param>
/// <param name="Json">The dictionary, in the form <see cref="FieldDictionary.Parse"/> reads.</param>
public sealed record DictionaryStartingPoint(string Name, string Description, string Json);

/// <summary>What came of saving a dictionary or a sample recipient.</summary>
/// <param name="Saved">Whether it was saved.</param>
/// <param name="Problems">Why it was not.</param>
public sealed record DictionarySaveResult(bool Saved, IReadOnlyList<Diagnostic> Problems);
