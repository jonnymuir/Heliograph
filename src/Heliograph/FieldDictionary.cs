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
    private readonly Dictionary<Field, Field> _listOf = new(ReferenceEqualityComparer.Instance);

    public FieldDictionary(IEnumerable<Field> fields)
    {
        ArgumentNullException.ThrowIfNull(fields);
        Fields = [.. fields];
        Add(Fields, list: null);
    }

    /// <summary>The fields a template can use directly. A list's own fields are in its <see cref="Field.Fields"/>.</summary>
    public IReadOnlyList<Field> Fields { get; }

    /// <summary>Every field an author can name in a tag: the top-level ones, and the fields of each list.</summary>
    public IEnumerable<Field> AllFields => Fields.Concat(Fields.SelectMany(list => list.Fields ?? []));

    /// <summary>Finds a field by the name an author writes, ignoring capitals and extra spaces.</summary>
    public Field? Find(string name) => _byName.GetValueOrDefault(FieldName.Normalise(name));

    /// <summary>The list a field belongs to, or null for a field that is not part of a list.</summary>
    public Field? ListOf(Field field) => _listOf.GetValueOrDefault(field);

    /// <summary>
    /// Names are unique across the whole dictionary, lists included, so that an author never has to say which
    /// "Date" they mean. Keys are unique where the data holds them: at the top, and within each list's items.
    /// </summary>
    private void Add(IReadOnlyList<Field> fields, Field? list)
    {
        if (fields.GroupBy(field => field.Key).FirstOrDefault(group => group.Count() > 1) is { } sameKey)
        {
            throw new ArgumentException($"The dictionary has two fields with the key \"{sameKey.Key}\".", nameof(fields));
        }

        foreach (var field in fields)
        {
            if (!_byName.TryAdd(FieldName.Normalise(field.Name), field))
            {
                throw new ArgumentException($"The dictionary has two fields called \"{field.Name}\".", nameof(fields));
            }

            CheckShape(field, list);
            if (list is not null)
            {
                _listOf[field] = list;
            }

            if (field.Type == FieldType.List)
            {
                Add(field.Fields!, field);
            }
        }
    }

    private static void CheckShape(Field field, Field? list)
    {
        var isList = field.Type == FieldType.List;
        if (isList && list is not null)
        {
            throw new ArgumentException($"The list \"{list.Name}\" holds another list, \"{field.Name}\". A list cannot hold a list.");
        }

        if (isList != field.Fields is { Count: > 0 })
        {
            throw new ArgumentException(isList
                ? $"The list \"{field.Name}\" has no fields of its own."
                : $"\"{field.Name}\" has fields of its own, which only a list may have.");
        }
    }

    /// <summary>Reads a dictionary from JSON: <c>{ "fields": [ { "name", "key", "type" } ] }</c>. A list has "fields" of its own.</summary>
    public static FieldDictionary Parse(string json)
    {
        try
        {
            var file = JsonSerializer.Deserialize<DictionaryFile>(json, JsonOptions);
            var fields = file?.Fields ?? throw new FormatException("The dictionary has no \"fields\" list.");
            var all = fields.Concat(fields.SelectMany(field => field.Fields ?? []));
            if (all.FirstOrDefault(f => string.IsNullOrWhiteSpace(f.Name) || string.IsNullOrWhiteSpace(f.Key)) is { } incomplete)
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

/// <summary>
/// One thing a template may use: the name an author writes in a tag, the key the data carries it under, and its
/// type. A list also has <paramref name="Fields"/>: what each of its items holds.
/// </summary>
public sealed record Field(string Name, string Key, FieldType Type, IReadOnlyList<Field>? Fields = null);

/// <summary>What kind of value a field holds. The type decides how the value is printed.</summary>
public enum FieldType
{
    /// <summary>Text, printed as it was typed: a line break stays a line break, and a blank line starts a new paragraph.</summary>
    Text,

    /// <summary>A date, supplied as an ISO date (2027-06-19) and printed in words (19 June 2027).</summary>
    Date,

    /// <summary>A postal address, supplied as a list of lines and printed one line under another.</summary>
    Address,

    /// <summary>A number, supplied as a number and printed with the thousands separated (12,500).</summary>
    Number,

    /// <summary>
    /// An amount of money, supplied as a number and printed to two decimal places (45.00). It prints no currency
    /// sign: the author writes that beside the tag, so it is never chosen or changed for them.
    /// </summary>
    Money,

    /// <summary>
    /// A list of items, supplied as an array of objects. It is never printed itself: a table row that uses the
    /// list's fields is repeated once for each item.
    /// </summary>
    List,

    /// <summary>
    /// Yes or no, supplied as true or false. It is never printed: a template shows or hides content on it. Where a
    /// letter needs the words, the data supplies them as text.
    /// </summary>
    YesNo,
}
