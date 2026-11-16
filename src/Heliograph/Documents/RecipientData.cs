using System.Text.Json;

namespace Heliograph.Documents;

/// <summary>
/// The data for one recipient, read field by field, with everything found wrong along the way. A field is reported
/// once however many times the template uses it. A field that belongs to a list is read from the item its row is for.
/// </summary>
internal sealed class RecipientData(JsonElement data, RenderSettings settings, FieldDictionary dictionary)
{
    private readonly Dictionary<string, Diagnostic> _diagnostics = [];

    public IReadOnlyList<Diagnostic> Diagnostics => [.. _diagnostics.Values];

    /// <summary>
    /// The paragraphs to print for a field, or null, with a diagnostic, when its value is missing or of the wrong
    /// type. A field that belongs to a list is read from <paramref name="item"/>, the item its row or line is for.
    /// </summary>
    public IReadOnlyList<string[]>? Paragraphs(Field field, ItemScope? item)
    {
        var source = Source(field, item);
        if (Value(field, source) is not { } value)
        {
            return null;
        }

        var paragraphs = ValueFormat.Paragraphs(field, value, settings);
        if (paragraphs is null)
        {
            WrongType(field, source);
        }

        return paragraphs;
    }

    /// <summary>Whether a yes or no field is yes. Anything else, including a missing or wrong value, is not, and is reported.</summary>
    public bool IsYes(Field field, ItemScope? item)
    {
        var source = Source(field, item);
        var kind = Value(field, source)?.ValueKind;
        if (kind is not (null or JsonValueKind.True or JsonValueKind.False))
        {
            WrongType(field, source);
        }

        return kind == JsonValueKind.True;
    }

    /// <summary>The items of a list, or null, with a diagnostic, when it is missing or is not a list of items.</summary>
    public IReadOnlyList<JsonElement>? Items(Field list)
    {
        var source = new Source(data, list.Key, "");
        if (Value(list, source) is not { } value)
        {
            return null;
        }

        if (value.ValueKind == JsonValueKind.Array && value.EnumerateArray().All(item => item.ValueKind == JsonValueKind.Object))
        {
            return [.. value.EnumerateArray()];
        }

        WrongType(list, source);
        return null;
    }

    /// <summary>Where a field's value is read from: the recipient's data, or one item of the list the field belongs to.</summary>
    private Source Source(Field field, ItemScope? item) => dictionary.ListOf(field) is { } list
        ? new Source(item!.Item, $"{list.Key}[{item.Number}].{field.Key}", $" in item {item.Number} of \"{list.Name}\"")
        : new Source(data, field.Key, "");

    private JsonElement? Value(Field field, Source source)
    {
        if (source.Holder.ValueKind == JsonValueKind.Object && source.Holder.TryGetProperty(field.Key, out var value))
        {
            if (value.ValueKind != JsonValueKind.Null)
            {
                return value;
            }
        }

        _diagnostics.TryAdd(source.Path, new Diagnostic(DiagnosticCodes.ValueMissing,
            $"The data has no value for \"{field.Name}\"{source.Where}.", $"Supply {source.Path} in the data."));
        return null;
    }

    private void WrongType(Field field, Source source) => _diagnostics.TryAdd(source.Path, new Diagnostic(DiagnosticCodes.ValueWrongType,
        $"The value for \"{field.Name}\"{source.Where} is not {ValueFormat.Described(field.Type)}.", ValueFormat.Expected(field)));
}

/// <summary>The object a value is read from, the path to the value for a developer, and where that is in words.</summary>
internal readonly record struct Source(JsonElement Holder, string Path, string Where);
