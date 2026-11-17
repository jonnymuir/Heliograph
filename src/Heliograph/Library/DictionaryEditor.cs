namespace Heliograph.Library;

/// <summary>
/// Adds and changes dictionaries and their sample recipients, through the host's <see cref="IDictionaryStore"/>. Each
/// change is checked first; a host whose source cannot be written to has every change refused.
/// </summary>
public sealed partial class DictionaryEditor(IDictionarySource source, IDictionaryStartingPoints? startingPoints = null)
{
    /// <summary>Whether authors can add and change dictionaries here, or only read them.</summary>
    public bool CanEdit => source is IDictionaryStore;

    /// <summary>The ready-made dictionaries the host offers as a place to start a new one. Empty when it offers none.</summary>
    public Task<IReadOnlyList<DictionaryStartingPoint>> StartingPointsAsync(CancellationToken cancellationToken = default) =>
        startingPoints?.ListAsync(cancellationToken) ?? Task.FromResult<IReadOnlyList<DictionaryStartingPoint>>([]);

    /// <summary>
    /// Adds or replaces a dictionary, once it reads as one. A published version keeps the dictionary it was published
    /// with, so only drafts see the change: <see cref="CorrespondenceLibrary.CheckDictionaryAsync"/> says what it breaks.
    /// </summary>
    public async Task<DictionarySaveResult> SaveAsync(string name, string json, CancellationToken cancellationToken = default)
    {
        if (Refusal(name) is { } refused)
        {
            return refused;
        }

        try
        {
            FieldDictionary.Parse(json);
        }
        catch (FormatException e)
        {
            return Refused(DiagnosticCodes.DictionaryNotValid, $"The dictionary cannot be saved. {e.Message}", "Correct the fields and save again.");
        }

        await ((IDictionaryStore)source).SaveAsync(name, json, cancellationToken).ConfigureAwait(false);
        return new DictionarySaveResult(true, []);
    }

    /// <summary>Adds or replaces a made-up sample recipient: a JSON object keyed by the dictionary's field keys.</summary>
    public async Task<DictionarySaveResult> SaveSampleAsync(string name, string sample, string json, CancellationToken cancellationToken = default)
    {
        if ((Refusal(name) ?? Refusal(sample)) is { } refused)
        {
            return refused;
        }

        if (!IsObject(json))
        {
            return Refused(DiagnosticCodes.SampleNotValid, "A sample recipient must be a JSON object, such as { \"memberName\": \"Alex\" }.", "Correct it and save again.");
        }

        await ((IDictionaryStore)source).SaveSampleAsync(name, sample, json, cancellationToken).ConfigureAwait(false);
        return new DictionarySaveResult(true, []);
    }

    /// <summary>Removes a sample recipient.</summary>
    public async Task<DictionarySaveResult> DeleteSampleAsync(string name, string sample, CancellationToken cancellationToken = default)
    {
        if ((Refusal(name) ?? Refusal(sample)) is { } refused)
        {
            return refused;
        }

        await ((IDictionaryStore)source).DeleteSampleAsync(name, sample, cancellationToken).ConfigureAwait(false);
        return new DictionarySaveResult(true, []);
    }

    /// <summary>Why nothing can be written under this name, or null when it can.</summary>
    private DictionarySaveResult? Refusal(string? name) =>
        !CanEdit ? Refused(DiagnosticCodes.DictionariesReadOnly, "Dictionaries cannot be changed here: this site takes them from elsewhere.", "Ask whoever maintains the data to change the dictionary.")
        : name is null || !NamePattern().IsMatch(name) ? Refused(DiagnosticCodes.DictionaryNameNotValid, $"\"{name}\" cannot be used as a name.", "Use letters, digits and hyphens, starting with a letter or digit, such as \"licensing\".")
        : null;

    private static bool IsObject(string json)
    {
        try
        {
            using var document = System.Text.Json.JsonDocument.Parse(json);
            return document.RootElement.ValueKind == System.Text.Json.JsonValueKind.Object;
        }
        catch (System.Text.Json.JsonException)
        {
            return false;
        }
    }

    private static DictionarySaveResult Refused(string code, string message, string fix) => new(false, [new Diagnostic(code, message, fix)]);

    [System.Text.RegularExpressions.GeneratedRegex("^[A-Za-z0-9][A-Za-z0-9-]*$")]
    private static partial System.Text.RegularExpressions.Regex NamePattern();
}
