using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Heliograph;

/// <summary>The closed list of everything a correspondence may refer to. See docs/brief.md, "The dictionary".</summary>
[SuppressMessage("Naming", "CA1711:Identifiers should not have incorrect suffix",
    Justification = "\"Dictionary\" is the product's own word for this: the list of what a template may use.")]
public sealed class DataDictionary
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase, allowIntegerValues: false) },
    };

    private readonly Dictionary<string, DictionaryEntry> _byName = new(StringComparer.Ordinal);

    public DataDictionary(IEnumerable<DictionaryEntry> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);
        Entries = [.. entries];
        foreach (var entry in Entries)
        {
            if (!_byName.TryAdd(FieldName.Normalise(entry.Name), entry))
            {
                throw new ArgumentException($"The dictionary has two entries called \"{entry.Name}\".", nameof(entries));
            }
        }
    }

    public IReadOnlyList<DictionaryEntry> Entries { get; }

    /// <summary>Finds an entry by the name an author writes, ignoring capitals and extra spaces.</summary>
    public DictionaryEntry? Find(string name) => _byName.GetValueOrDefault(FieldName.Normalise(name));

    /// <summary>Reads a dictionary from JSON: <c>{ "entries": [ { "name", "key", "type" } ] }</c>.</summary>
    public static DataDictionary Parse(string json)
    {
        try
        {
            var file = JsonSerializer.Deserialize<DictionaryFile>(json, JsonOptions);
            var entries = file?.Entries ?? throw new FormatException("The dictionary has no \"entries\" list.");
            if (entries.FirstOrDefault(e => string.IsNullOrWhiteSpace(e.Name) || string.IsNullOrWhiteSpace(e.Key)) is { } incomplete)
            {
                throw new FormatException($"A dictionary entry needs a name and a key: \"{incomplete.Name}\", \"{incomplete.Key}\".");
            }

            return new DataDictionary(entries);
        }
        catch (JsonException e)
        {
            throw new FormatException($"The dictionary is not valid: {e.Message}", e);
        }
        catch (ArgumentException e)
        {
            throw new FormatException(e.Message, e);
        }
    }

    private sealed record DictionaryFile(IReadOnlyList<DictionaryEntry>? Entries);
}

/// <summary>One thing a template may use: the name an author writes, the key the data carries it under, and its type.</summary>
public sealed record DictionaryEntry(string Name, string Key, EntryType Type);

/// <summary>What kind of value an entry holds. The type decides how the value is printed.</summary>
public enum EntryType
{
    /// <summary>Text, printed as supplied. A new line in the text becomes a line break.</summary>
    Text,

    /// <summary>A date, supplied as an ISO date (2027-06-19) and printed in words (19 June 2027).</summary>
    Date,

    /// <summary>A postal address, supplied as a list of lines and printed one line under another.</summary>
    Address,
}
