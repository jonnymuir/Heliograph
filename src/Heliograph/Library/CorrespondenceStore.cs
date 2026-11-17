namespace Heliograph.Library;

/// <summary>
/// What an author keeps for one correspondence: its name, the dictionary it draws on, and a part for each channel
/// it is sent on.
/// </summary>
public sealed class CorrespondenceContent
{
    /// <summary>What people call it, such as "Decision letter".</summary>
    public required string Name { get; init; }

    /// <summary>The name of the dictionary every part is checked against.</summary>
    public required string Dictionary { get; init; }

    /// <summary>The document part: a .docx, or null if there is none.</summary>
#pragma warning disable CA1819 // The file is a block of bytes; a copy per read would only cost memory.
    public byte[]? Document { get; init; }
#pragma warning restore CA1819

    /// <summary>The email part: a first line "Subject: ...", then the body. Null if there is none.</summary>
    public string? Email { get; init; }

    /// <summary>The SMS part, or null if there is none.</summary>
    public string? Sms { get; init; }
}

/// <summary>The copy of a correspondence an author is working on. It may not compile yet.</summary>
/// <param name="Key">The correspondence's key.</param>
/// <param name="Content">What the author has saved.</param>
/// <param name="Revision">Goes up by one with every save, so a save can tell that someone else saved first.</param>
public sealed record CorrespondenceDraft(string Key, CorrespondenceContent Content, int Revision);

/// <summary>
/// A published version of a correspondence. It never changes: it keeps the content and the dictionary exactly as
/// they were when it was published, so it renders the same for as long as it exists.
/// </summary>
/// <param name="Key">The correspondence's key.</param>
/// <param name="Version">The version's number, counting from 1.</param>
/// <param name="Content">The content that was published.</param>
/// <param name="DictionaryJson">The dictionary as it was when the version was published.</param>
/// <param name="PublishedAt">When it was published.</param>
public sealed record PublishedCorrespondence(
    string Key, int Version, CorrespondenceContent Content, string DictionaryJson, DateTimeOffset PublishedAt);

/// <summary>One line of the list of correspondence.</summary>
/// <param name="Key">The correspondence's key.</param>
/// <param name="Name">What people call it.</param>
/// <param name="Dictionary">The name of the dictionary its draft uses.</param>
/// <param name="LatestVersion">The newest published version, or null if it has never been published.</param>
public sealed record CorrespondenceSummary(string Key, string Name, string Dictionary, int? LatestVersion);

/// <summary>
/// Where correspondence is kept. This is a seam: the host supplies it (files, a database, a document library), and
/// it only keeps and returns what it is given. The rules are in <see cref="CorrespondenceLibrary"/>.
/// </summary>
public interface ICorrespondenceStore
{
    /// <summary>Every correspondence that has a draft, in order of key.</summary>
    Task<IReadOnlyList<CorrespondenceSummary>> ListAsync(CancellationToken cancellationToken = default);

    /// <summary>The draft, or null if there is no correspondence with this key.</summary>
    Task<CorrespondenceDraft?> ReadDraftAsync(string key, CancellationToken cancellationToken = default);

    /// <summary>
    /// Saves the draft if its revision is still <paramref name="expectedRevision"/> (0 for a correspondence that does
    /// not exist yet), and returns the new revision. Returns null, saving nothing, if it is not. The check and the
    /// save must be one step, so that two people saving at once cannot both succeed.
    /// </summary>
    Task<int?> SaveDraftAsync(string key, CorrespondenceContent content, int expectedRevision, CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds a published version with the next number, counting from 1, and returns it. Choosing the number and
    /// storing the version must be one step.
    /// </summary>
    Task<PublishedCorrespondence> AddVersionAsync(
        string key, CorrespondenceContent content, string dictionaryJson, DateTimeOffset publishedAt, CancellationToken cancellationToken = default);

    /// <summary>The published version with this number, or the newest when no number is given. Null if there is none.</summary>
    Task<PublishedCorrespondence?> ReadVersionAsync(string key, int? version = null, CancellationToken cancellationToken = default);
}

/// <summary>
/// Where dictionaries come from. This is a seam: a dictionary belongs to whoever knows the data, so a host supplies
/// it from a file, from a Wayfinder blueprint, or from any system that can describe its own fields.
/// </summary>
public interface IDictionarySource
{
    /// <summary>The names of the dictionaries a correspondence can use, in order.</summary>
    Task<IReadOnlyList<string>> ListAsync(CancellationToken cancellationToken = default);

    /// <summary>The dictionary as JSON, in the form <see cref="FieldDictionary.Parse"/> reads, or null if there is none with this name.</summary>
    Task<string?> ReadAsync(string name, CancellationToken cancellationToken = default);

    /// <summary>
    /// The names of the made-up recipients a dictionary has for previews, in order. Samples belong with the dictionary
    /// because they are written by whoever knows the data. A source with none returns an empty list.
    /// </summary>
    Task<IReadOnlyList<string>> ListSamplesAsync(string name, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<string>>([]);

    /// <summary>One sample recipient's data as a JSON object, or null if there is none with this name.</summary>
    Task<string?> ReadSampleAsync(string name, string sample, CancellationToken cancellationToken = default) =>
        Task.FromResult<string?>(null);
}
