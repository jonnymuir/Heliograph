using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Heliograph.Library;

/// <summary>
/// Correspondence kept as files in a folder, a folder for each key, with the parts as ordinary files a person can
/// open: <c>document.docx</c>, <c>email.md</c> and <c>sms.txt</c>. The draft is in <c>draft/</c> and each published
/// version in <c>versions/&lt;number&gt;/</c>. The smallest store a host can use, and the one the reference app uses.
/// </summary>
/// <remarks>
/// It is safe for many requests in one process, which is all a reference app or a small site needs. It does not
/// coordinate between processes; a host with several servers keeps correspondence in a database.
/// </remarks>
public sealed partial class FolderCorrespondenceStore(string folder) : ICorrespondenceStore, IDisposable
{
    private const string DocumentFile = "document.docx";
    private const string EmailFile = "email.md";
    private const string SmsFile = "sms.txt";
    private const string AboutFile = "correspondence.json";
    private const string DictionaryFile = "dictionary.json";

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    private readonly SemaphoreSlim _gate = new(1, 1);

    public void Dispose() => _gate.Dispose();

    public async Task<IReadOnlyList<CorrespondenceSummary>> ListAsync(CancellationToken cancellationToken = default)
    {
        var summaries = new List<CorrespondenceSummary>();
        foreach (var key in Keys())
        {
            if (await ReadDraftAsync(key, cancellationToken).ConfigureAwait(false) is { } draft)
            {
                summaries.Add(new CorrespondenceSummary(key, draft.Content.Name, draft.Content.Dictionary, LatestVersion(key)));
            }
        }

        return summaries;
    }

    public async Task<CorrespondenceDraft?> ReadDraftAsync(string key, CancellationToken cancellationToken = default)
    {
        if (!IsKey(key) || await ReadAboutAsync(Draft(key), cancellationToken).ConfigureAwait(false) is not { } about)
        {
            return null;
        }

        return new CorrespondenceDraft(key, await ReadContentAsync(Draft(key), about, cancellationToken).ConfigureAwait(false), about.Revision);
    }

    public async Task<int?> SaveDraftAsync(string key, CorrespondenceContent content, int expectedRevision, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(content);
        if (!IsKey(key))
        {
            return null;
        }

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var current = (await ReadAboutAsync(Draft(key), cancellationToken).ConfigureAwait(false))?.Revision ?? 0;
            if (current != expectedRevision)
            {
                return null;
            }

            await WriteContentAsync(Draft(key), content, new About(content.Name, content.Dictionary, current + 1, null), cancellationToken).ConfigureAwait(false);
            return current + 1;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<PublishedCorrespondence> AddVersionAsync(
        string key, CorrespondenceContent content, string dictionaryJson, DateTimeOffset publishedAt, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(content);
        if (!IsKey(key))
        {
            throw new ArgumentException($"\"{key}\" cannot be used as a key.", nameof(key));
        }

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var version = (LatestVersion(key) ?? 0) + 1;
            var into = Version(key, version);
            await WriteContentAsync(into, content, new About(content.Name, content.Dictionary, 0, publishedAt), cancellationToken).ConfigureAwait(false);
            await File.WriteAllTextAsync(Path.Combine(into, DictionaryFile), dictionaryJson, cancellationToken).ConfigureAwait(false);
            return new PublishedCorrespondence(key, version, content, dictionaryJson, publishedAt);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<PublishedCorrespondence?> ReadVersionAsync(string key, int? version = null, CancellationToken cancellationToken = default)
    {
        if (!IsKey(key) || (version ?? LatestVersion(key)) is not { } number
            || await ReadAboutAsync(Version(key, number), cancellationToken).ConfigureAwait(false) is not { PublishedAt: { } publishedAt } about)
        {
            return null;
        }

        var from = Version(key, number);
        var content = await ReadContentAsync(from, about, cancellationToken).ConfigureAwait(false);
        var dictionary = await File.ReadAllTextAsync(Path.Combine(from, DictionaryFile), cancellationToken).ConfigureAwait(false);
        return new PublishedCorrespondence(key, number, content, dictionary, publishedAt);
    }

    private IEnumerable<string> Keys() => Directory.Exists(folder)
        ? Directory.GetDirectories(folder).Select(Path.GetFileName).OfType<string>().Where(IsKey).Order(StringComparer.Ordinal)
        : [];

    private int? LatestVersion(string key)
    {
        var versions = Path.Combine(folder, key, "versions");
        return Directory.Exists(versions)
            ? Directory.GetDirectories(versions)
                .Select(path => int.TryParse(Path.GetFileName(path), NumberStyles.None, CultureInfo.InvariantCulture, out var n) ? n : 0)
                .Where(n => n > 0).DefaultIfEmpty().Max() is var latest and > 0 ? latest : null
            : null;
    }

    private string Draft(string key) => Path.Combine(folder, key, "draft");

    private string Version(string key, int version) => Path.Combine(folder, key, "versions", version.ToString(CultureInfo.InvariantCulture));

    private static async Task<About?> ReadAboutAsync(string from, CancellationToken cancellationToken)
    {
        var path = Path.Combine(from, AboutFile);
        if (!File.Exists(path))
        {
            return null;
        }

        await using var stream = File.OpenRead(path);
        return await JsonSerializer.DeserializeAsync<About>(stream, Json, cancellationToken).ConfigureAwait(false);
    }

    private static async Task<CorrespondenceContent> ReadContentAsync(string from, About about, CancellationToken cancellationToken) => new()
    {
        Name = about.Name,
        Dictionary = about.Dictionary,
        Document = File.Exists(Path.Combine(from, DocumentFile)) ? await File.ReadAllBytesAsync(Path.Combine(from, DocumentFile), cancellationToken).ConfigureAwait(false) : null,
        Email = File.Exists(Path.Combine(from, EmailFile)) ? await File.ReadAllTextAsync(Path.Combine(from, EmailFile), cancellationToken).ConfigureAwait(false) : null,
        Sms = File.Exists(Path.Combine(from, SmsFile)) ? await File.ReadAllTextAsync(Path.Combine(from, SmsFile), cancellationToken).ConfigureAwait(false) : null,
    };

    private static async Task WriteContentAsync(string into, CorrespondenceContent content, About about, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(into);
        await WriteOrDeleteAsync(Path.Combine(into, DocumentFile), content.Document, cancellationToken).ConfigureAwait(false);
        await WriteOrDeleteAsync(Path.Combine(into, EmailFile), content.Email is null ? null : System.Text.Encoding.UTF8.GetBytes(content.Email), cancellationToken).ConfigureAwait(false);
        await WriteOrDeleteAsync(Path.Combine(into, SmsFile), content.Sms is null ? null : System.Text.Encoding.UTF8.GetBytes(content.Sms), cancellationToken).ConfigureAwait(false);

        // Written last: a draft whose parts are half written is not yet the new revision.
        await File.WriteAllTextAsync(Path.Combine(into, AboutFile), JsonSerializer.Serialize(about, Json), cancellationToken).ConfigureAwait(false);
    }

    private static async Task WriteOrDeleteAsync(string path, byte[]? bytes, CancellationToken cancellationToken)
    {
        if (bytes is null)
        {
            File.Delete(path);
        }
        else
        {
            await File.WriteAllBytesAsync(path, bytes, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>A key becomes part of a path. The library refuses a bad one first; this makes sure of it.</summary>
    private static bool IsKey(string? key) => key is not null && KeyPattern().IsMatch(key);

    [GeneratedRegex("^[a-z][a-z0-9]*(-[a-z0-9]+)*$")]
    private static partial Regex KeyPattern();

    private sealed record About(string Name, string Dictionary, int Revision, DateTimeOffset? PublishedAt);
}
