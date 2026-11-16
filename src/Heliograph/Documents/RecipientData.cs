using System.Text.Json;

namespace Heliograph.Documents;

/// <summary>
/// The data for one recipient, read field by field, with everything found wrong along the way. A field is reported
/// once however many times the template uses it.
/// </summary>
internal sealed class RecipientData(JsonElement data, RenderSettings settings)
{
    private readonly Dictionary<string, Diagnostic> _diagnostics = [];

    public IReadOnlyList<Diagnostic> Diagnostics => [.. _diagnostics.Values];

    /// <summary>The lines to print for a field, or null, with a diagnostic, when its value is missing or of the wrong type.</summary>
    public IReadOnlyList<string>? Lines(Field field)
    {
        if (Value(field) is not { } value)
        {
            return null;
        }

        var lines = ValueWriter.Lines(field, value, settings);
        if (lines is null)
        {
            WrongType(field);
        }

        return lines;
    }

    /// <summary>Whether a yes or no field is yes. Anything else, including a missing or wrong value, is not, and is reported.</summary>
    public bool IsYes(Field field)
    {
        var kind = Value(field)?.ValueKind;
        if (kind is not (null or JsonValueKind.True or JsonValueKind.False))
        {
            WrongType(field);
        }

        return kind == JsonValueKind.True;
    }

    private JsonElement? Value(Field field)
    {
        if (data.ValueKind == JsonValueKind.Object && data.TryGetProperty(field.Key, out var value))
        {
            if (value.ValueKind != JsonValueKind.Null)
            {
                return value;
            }
        }

        _diagnostics.TryAdd(field.Key, new Diagnostic(DiagnosticCodes.ValueMissing,
            $"The data has no value for \"{field.Name}\".", $"Supply {field.Key} in the data."));
        return null;
    }

    private void WrongType(Field field) => _diagnostics.TryAdd(field.Key, new Diagnostic(DiagnosticCodes.ValueWrongType,
        $"The value for \"{field.Name}\" is not {ValueWriter.Described(field.Type)}.", ValueWriter.Expected(field)));
}
