using System.ComponentModel;
using Heliograph.Library;
using ModelContextProtocol;
using ModelContextProtocol.Server;

namespace Heliograph.Mcp;

/// <summary>
/// Keeping dictionaries over MCP: the same operations the workbench's Dictionaries screen offers, with the same
/// checks, so an agent can add a field a letter needs and see what the change breaks.
/// </summary>
[McpServerToolType]
public static class DictionaryTools
{
    [McpServerTool(Name = "read_dictionary_definition", ReadOnly = true)]
    [Description("Read a dictionary as the JSON it is kept in, to change it and save it back with save_dictionary.")]
    public static async Task<string> ReadDictionaryDefinition(
        CorrespondenceLibrary library,
        [Description("The dictionary's name.")] string name,
        CancellationToken ct)
    {
        return await library.Dictionaries.ReadAsync(name, ct).ConfigureAwait(false)
            ?? throw new McpException($"There is no dictionary called \"{name}\". Call list_dictionaries to see the names that exist.");
    }

    [McpServerTool(Name = "list_dictionary_starting_points", ReadOnly = true)]
    [Description("List the ready-made dictionaries this site offers as a place to start a new one, such as one built from a service blueprint's fields, and whether dictionaries can be changed here at all.")]
    public static async Task<DictionarySettingsView> ListDictionaryStartingPoints(CorrespondenceLibrary library, CancellationToken ct)
    {
        return new DictionarySettingsView(library.Dictionaries.Editing.CanEdit, await library.Dictionaries.Editing.StartingPointsAsync(ct).ConfigureAwait(false));
    }

    [McpServerTool(Name = "save_dictionary", Destructive = false)]
    [Description(
        "Add or replace a dictionary: JSON of the form { \"fields\": [ { \"name\": \"Member name\", \"key\": \"memberName\", \"type\": \"text\" } ] }. " +
        "Types are text, date, address, number, money, yesNo and list; a list has \"fields\" of its own. Returns what the change now " +
        "means for every draft that uses the dictionary. Published versions keep the dictionary they were published with. " +
        "Only change a dictionary when the person asks you to: it belongs to whoever knows the data.")]
    public static async Task<DictionarySaveView> SaveDictionary(
        CorrespondenceLibrary library,
        [Description("The dictionary's name: letters, digits and hyphens.")] string name,
        [Description("The whole dictionary, as JSON.")] string json,
        CancellationToken ct)
    {
        var save = await library.Dictionaries.Editing.SaveAsync(name, json, ct).ConfigureAwait(false);
        return save.Saved
            ? new DictionarySaveView(true, [], [.. (await library.CheckDictionaryAsync(name, ct).ConfigureAwait(false)).Select(u => new DictionaryUseView(u.Key, u.Name, Problems(u.Diagnostics)))])
            : new DictionarySaveView(false, Problems(save.Problems), []);
    }

    [McpServerTool(Name = "save_sample", Destructive = false)]
    [Description("Add or replace one of a dictionary's made-up sample recipients, used by preview_correspondence: a JSON object keyed by the fields' keys. Invent people; never use real ones.")]
    public static async Task<IReadOnlyList<ProblemView>> SaveSample(
        CorrespondenceLibrary library,
        [Description("The dictionary's name.")] string dictionary,
        [Description("The sample's name, such as \"approved\".")] string sample,
        [Description("The sample recipient's data, as a JSON object.")] string json,
        CancellationToken ct)
    {
        return Problems((await library.Dictionaries.Editing.SaveSampleAsync(dictionary, sample, json, ct).ConfigureAwait(false)).Problems);
    }

    private static IReadOnlyList<ProblemView> Problems(IReadOnlyList<Diagnostic> diagnostics) => [.. diagnostics.Select(d => new ProblemView(
        d.Code, d.Severity == DiagnosticSeverity.Error ? "error" : "warning", d.Message, d.Fix, d.Location?.Part, d.Location?.Paragraph, d.Location?.Excerpt))];
}

public sealed record DictionarySettingsView(bool CanEdit, IReadOnlyList<DictionaryStartingPoint> StartingPoints);

public sealed record DictionarySaveView(bool Saved, IReadOnlyList<ProblemView> Problems, IReadOnlyList<DictionaryUseView> Uses);
