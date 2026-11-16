using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Heliograph.Library;

/// <summary>
/// The operations on stored correspondence, the same for a person at a screen, a program and an agent: list, read,
/// save a draft, check it, preview it, publish it, and render a published version for a recipient.
/// </summary>
/// <remarks>
/// An author works on a draft. Publishing makes a numbered version that never changes, and only a published version
/// is rendered for a real recipient. The library keeps nothing itself: correspondence comes from the host's
/// <see cref="ICorrespondenceStore"/> and dictionaries from its <see cref="IDictionarySource"/>.
/// </remarks>
public sealed partial class CorrespondenceLibrary(ICorrespondenceStore store, IDictionarySource dictionaries, TimeProvider clock)
{
    // A published version never changes, so one compiled is good for as long as the process runs.
    private readonly ConcurrentDictionary<(string Key, int Version), CompiledCorrespondence> _published = new();

    /// <summary>Every correspondence, in order of key.</summary>
    public Task<IReadOnlyList<CorrespondenceSummary>> ListAsync(CancellationToken cancellationToken = default) =>
        store.ListAsync(cancellationToken);

    /// <summary>The names of the dictionaries a correspondence can use.</summary>
    public Task<IReadOnlyList<string>> ListDictionariesAsync(CancellationToken cancellationToken = default) =>
        dictionaries.ListAsync(cancellationToken);

    /// <summary>A dictionary as JSON, or null if there is none with this name.</summary>
    public Task<string?> ReadDictionaryAsync(string name, CancellationToken cancellationToken = default) =>
        dictionaries.ReadAsync(name, cancellationToken);

    /// <summary>The draft an author is working on, or null if there is no correspondence with this key.</summary>
    public Task<CorrespondenceDraft?> ReadDraftAsync(string key, CancellationToken cancellationToken = default) =>
        store.ReadDraftAsync(key, cancellationToken);

    /// <summary>A published version, or the newest when no number is given. Null if there is none.</summary>
    public Task<PublishedCorrespondence?> ReadVersionAsync(string key, int? version = null, CancellationToken cancellationToken = default) =>
        store.ReadVersionAsync(key, version, cancellationToken);

    /// <summary>
    /// Saves a draft and says what is wrong with it. A draft that does not compile is still saved. Pass the revision
    /// the draft had when it was read, or 0 for a new correspondence; if someone else has saved since, nothing is
    /// saved and the diagnostics say so.
    /// </summary>
    public async Task<SaveResult> SaveDraftAsync(
        string key, CorrespondenceContent content, int expectedRevision, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(content);
        if (key is null || !KeyPattern().IsMatch(key))
        {
            return new SaveResult(null, [Problem(DiagnosticCodes.KeyNotValid,
                $"\"{key}\" cannot be used as a key.",
                "Use lower-case letters, digits and hyphens, starting with a letter, such as \"decision-letter\".")]);
        }

        if (await store.SaveDraftAsync(key, content, expectedRevision, cancellationToken).ConfigureAwait(false) is not { } revision)
        {
            return new SaveResult(null, [Problem(DiagnosticCodes.ChangedElsewhere,
                "This correspondence has been saved by someone else since you read it, so your copy was not saved.",
                "Read it again, make your change to that copy, and save it.")]);
        }

        var (_, diagnostics) = await CompileAsync(content, dictionaryJson: null, cancellationToken).ConfigureAwait(false);
        return new SaveResult(revision, diagnostics);
    }

    /// <summary>Compiles the draft and says what is wrong with it.</summary>
    public async Task<CheckResult> CheckAsync(string key, CancellationToken cancellationToken = default)
    {
        if (await store.ReadDraftAsync(key, cancellationToken).ConfigureAwait(false) is not { } draft)
        {
            return new CheckResult([NotFound(key)]);
        }

        var (_, diagnostics) = await CompileAsync(draft.Content, dictionaryJson: null, cancellationToken).ConfigureAwait(false);
        return new CheckResult(diagnostics);
    }

    /// <summary>Renders the draft with sample data, so an author sees each part as a recipient would get it.</summary>
    public async Task<LibraryRender> PreviewAsync(
        string key, JsonElement data, RenderSettings settings, CancellationToken cancellationToken = default)
    {
        if (await store.ReadDraftAsync(key, cancellationToken).ConfigureAwait(false) is not { } draft)
        {
            return new LibraryRender(null, null, [NotFound(key)]);
        }

        var (compiled, diagnostics) = await CompileAsync(draft.Content, dictionaryJson: null, cancellationToken).ConfigureAwait(false);
        return compiled is null ? new LibraryRender(null, null, diagnostics) : Render(compiled, version: null, data, settings);
    }

    /// <summary>
    /// Publishes the draft as the next numbered version. A draft with an error is refused. The version keeps the
    /// dictionary as it is now, so a later change to the dictionary cannot change what the version renders.
    /// </summary>
    public async Task<PublishResult> PublishAsync(string key, CancellationToken cancellationToken = default)
    {
        if (await store.ReadDraftAsync(key, cancellationToken).ConfigureAwait(false) is not { } draft)
        {
            return new PublishResult(null, [NotFound(key)]);
        }

        var dictionaryJson = await dictionaries.ReadAsync(draft.Content.Dictionary, cancellationToken).ConfigureAwait(false);
        var (compiled, diagnostics) = await CompileAsync(draft.Content, dictionaryJson, cancellationToken).ConfigureAwait(false);
        if (compiled is null)
        {
            return new PublishResult(null, diagnostics);
        }

        var published = await store.AddVersionAsync(key, draft.Content, dictionaryJson!, clock.GetUtcNow(), cancellationToken).ConfigureAwait(false);
        _published[(key, published.Version)] = compiled;
        return new PublishResult(published.Version, diagnostics);
    }

    /// <summary>
    /// Renders a published version for one recipient: the newest, or the one named. The result says which version
    /// was used. A draft is never rendered here.
    /// </summary>
    public async Task<LibraryRender> RenderAsync(
        string key, JsonElement data, RenderSettings settings, int? version = null, CancellationToken cancellationToken = default)
    {
        if (await store.ReadVersionAsync(key, version, cancellationToken).ConfigureAwait(false) is not { } published)
        {
            return new LibraryRender(null, null, [Problem(DiagnosticCodes.NotPublished,
                version is null ? $"\"{key}\" has no published version." : $"\"{key}\" has no published version {version}.",
                "Publish the correspondence, or ask for a version that exists.")]);
        }

        if (!_published.TryGetValue((key, published.Version), out var compiled))
        {
            (compiled, var diagnostics) = await CompileAsync(published.Content, published.DictionaryJson, cancellationToken).ConfigureAwait(false);
            if (compiled is null)
            {
                // It compiled when it was published, so this means the engine has changed underneath it.
                return new LibraryRender(null, null, diagnostics);
            }

            _published[(key, published.Version)] = compiled;
        }

        return Render(compiled, published.Version, data, settings);
    }

    /// <summary>
    /// Compiles the draft of every correspondence that uses a dictionary against the dictionary as it is now, so a
    /// change to it shows what it breaks before anything is published.
    /// </summary>
    public async Task<IReadOnlyList<DictionaryUse>> CheckDictionaryAsync(string name, CancellationToken cancellationToken = default)
    {
        var uses = new List<DictionaryUse>();
        foreach (var summary in await store.ListAsync(cancellationToken).ConfigureAwait(false))
        {
            if (string.Equals(summary.Dictionary, name, StringComparison.Ordinal))
            {
                uses.Add(new DictionaryUse(summary.Key, summary.Name, (await CheckAsync(summary.Key, cancellationToken).ConfigureAwait(false)).Diagnostics));
            }
        }

        return uses;
    }

    private static LibraryRender Render(CompiledCorrespondence compiled, int? version, JsonElement data, RenderSettings settings)
    {
        var rendered = compiled.Render(data, settings);
        return rendered.Diagnostics.Count > 0
            ? new LibraryRender(null, null, rendered.Diagnostics)
            : new LibraryRender(version, rendered, []);
    }

    /// <summary>Compiles content against the dictionary given, or the one it names as that dictionary is now.</summary>
    private async Task<(CompiledCorrespondence? Compiled, IReadOnlyList<Diagnostic> Diagnostics)> CompileAsync(
        CorrespondenceContent content, string? dictionaryJson, CancellationToken cancellationToken)
    {
        if (content is { Document: null, Email: null, Sms: null })
        {
            return (null, [Problem(DiagnosticCodes.NoParts,
                "This correspondence has no parts.", "Add a document, an email or a text message.")]);
        }

        dictionaryJson ??= await dictionaries.ReadAsync(content.Dictionary, cancellationToken).ConfigureAwait(false);
        if (dictionaryJson is null)
        {
            return (null, [Problem(DiagnosticCodes.DictionaryNotFound,
                $"This correspondence uses a dictionary called \"{content.Dictionary}\", and there is none with that name.",
                "Choose one of the dictionaries that exist.")]);
        }

        FieldDictionary dictionary;
        try
        {
            dictionary = FieldDictionary.Parse(dictionaryJson);
        }
        catch (FormatException e)
        {
            return (null, [Problem(DiagnosticCodes.DictionaryNotValid,
                $"The dictionary \"{content.Dictionary}\" cannot be read. {e.Message}",
                "Whoever maintains the dictionary needs to correct it.")]);
        }

        using var document = content.Document is null ? null : new MemoryStream(content.Document, writable: false);
        var compilation = Correspondence.Compile(new CorrespondenceSource { Document = document, Email = content.Email, Sms = content.Sms }, dictionary);
        return (compilation.Correspondence, compilation.Diagnostics);
    }

    private static Diagnostic NotFound(string key) => Problem(DiagnosticCodes.CorrespondenceNotFound,
        $"There is no correspondence with the key \"{key}\".", "List the correspondence to see the keys that exist.");

    private static Diagnostic Problem(string code, string message, string fix) => new(code, message, fix);

    [GeneratedRegex("^[a-z][a-z0-9]*(-[a-z0-9]+)*$")]
    private static partial Regex KeyPattern();
}
