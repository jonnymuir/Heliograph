namespace Heliograph.Library;

/// <summary>A dictionary laid out for an author: each field with the tag to write for it, ready to copy.</summary>
/// <param name="Name">The dictionary's name.</param>
/// <param name="Fields">Every field a template may use, lists' fields included.</param>
public sealed record DictionarySheet(string Name, IReadOnlyList<DictionaryEntry> Fields)
{
    /// <summary>Lays a dictionary out for an author.</summary>
    public static DictionarySheet From(string name, FieldDictionary dictionary)
    {
        ArgumentNullException.ThrowIfNull(dictionary);
        return new DictionarySheet(name, [.. dictionary.AllFields.Select(field => new DictionaryEntry(
            field.Type switch
            {
                FieldType.YesNo => $"((if {field.Name}))",
                FieldType.List => null,
                _ => $"(({field.Name}))",
            },
            field.Name, field.Key, TypeName(field.Type), dictionary.ListOf(field)?.Name))]);
    }

    /// <summary>What a type is called in words an author uses.</summary>
    public static string TypeName(FieldType type) => type switch
    {
        FieldType.YesNo => "yes or no",
        _ => type.ToString().ToLowerInvariant(),
    };
}

/// <summary>One field, as an author needs to see it.</summary>
/// <param name="Tag">What to write in a template. Null for a list, which is never written itself.</param>
/// <param name="Name">The field's name.</param>
/// <param name="Key">The key the data carries the value under.</param>
/// <param name="Type">text, date, address, number, money, list, or yes or no.</param>
/// <param name="List">The list this field belongs to, or null.</param>
public sealed record DictionaryEntry(string? Tag, string Name, string Key, string Type, string? List);
