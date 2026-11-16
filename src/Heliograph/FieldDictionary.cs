using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Heliograph;

/// <summary>
/// The closed list of fields a correspondence may use. A field is a thing a template can refer to; a tag,
/// <c>((Member name))</c>, is how an author writes one. See docs/brief.md, "The dictionary".
/// </summary>
[SuppressMessage("Naming", "CA1711:Identifiers should not have incorrect suffix",
    Justification = "\"Dictionary\" is the product's own word for this: the list of what a template may use.")]
public sealed class FieldDictionary
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase, allowIntegerValues: false) },
    };

    private readonly Dictionary<string, Field> _byName = new(StringComparer.Ordinal);

    public FieldDictionary(IEnumerable<Field> fields)
    {
        ArgumentNullException.ThrowIfNull(fields);
        Fields = [.. fields];
        foreach (var field in Fields)
        {
            if (!_byName.TryAdd(FieldName.Normalise(field.Name), field))
            {
                throw new ArgumentException($"The dictionary has two fields called \"{field.Name}\".", nameof(fields));
            }
        }
    }

    public IReadOnlyList<Field> Fields { get; }

    /// <summary>Finds a field by the name an author writes, ignoring capitals and extra spaces.</summary>
    public Field? Find(string name) => _byName.GetValueOrDefault(FieldName.Normalise(name));

    /// <summary>Reads a dictionary from JSON: <c>{ "fields": [ { "name", "key", "type" } ] }</c>.</summary>
    public static FieldDictionary Parse(string json)
    {
        try
        {
            var file = JsonSerializer.Deserialize<DictionaryFile>(json, JsonOptions);
            var fields = file?.Fields ?? throw new FormatException("The dictionary has no \"fields\" list.");
            if (fields.FirstOrDefault(f => string.IsNullOrWhiteSpace(f.Name) || string.IsNullOrWhiteSpace(f.Key)) is { } incomplete)
            {
                throw new FormatException($"A field needs a name and a key: \"{incomplete.Name}\", \"{incomplete.Key}\".");
            }

            return new FieldDictionary(fields);
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

    private sealed record DictionaryFile(IReadOnlyList<Field>? Fields);
}

/// <summary>One thing a template may use: the name an author writes in a tag, the key the data carries it under, and its type.</summary>
public sealed record Field(string Name, string Key, FieldType Type);

/// <summary>What kind of value a field holds. The type decides how the value is printed.</summary>
public enum FieldType
{
    /// <summary>Text, printed as supplied. A new line in the text becomes a line break.</summary>
    Text,

    /// <summary>A date, supplied as an ISO date (2027-06-19) and printed in words (19 June 2027).</summary>
    Date,

    /// <summary>A postal address, supplied as a list of lines and printed one line under another.</summary>
    Address,

    /// <summary>A number, supplied as a number and printed with the thousands separated (12,500).</summary>
    Number,

    /// <summary>An amount of money, supplied as a number and printed with its currency and two decimal places (£45.00).</summary>
    Money,

    /// <summary>Yes or no, supplied as true or false. It is never printed: a template shows or hides content on it.</summary>
    YesNo,
}
