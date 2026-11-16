using System.Globalization;
using System.Text.Json;

namespace Heliograph.Documents;

/// <summary>
/// Turns one value from the data into the text to print, according to its field's type. The result is a list of
/// paragraphs, each a list of lines. Only text can be more than one paragraph.
/// </summary>
internal static class ValueFormat
{
    private static readonly string[] LineEndings = ["\r\n", "\n", "\r"];

    private static readonly Dictionary<FieldType, (string Described, string Supply)> Words = new()
    {
        [FieldType.Text] = ("text", "as text"),
        [FieldType.Date] = ("a date", "as a date in the form 2027-06-19"),
        [FieldType.Address] = ("an address", "as a list of address lines, each of them text"),
        [FieldType.Number] = ("a number", "as a number, such as 24"),
        [FieldType.Money] = ("an amount of money", "as a number, such as 45 or 45.5, with no currency sign"),
        [FieldType.YesNo] = ("true or false", "as true or false"),
        [FieldType.List] = ("a list of items", "as a list of items, each an object with the list's own fields"),
    };

    /// <summary>The paragraphs to print, or null when the value is not of the field's type.</summary>
    public static IReadOnlyList<string[]>? Paragraphs(Field field, JsonElement value, RenderSettings settings) => field.Type switch
    {
        FieldType.Text => Text(value),
        FieldType.Date => One(Date(value, settings)),
        FieldType.Address => One(Address(value)),
        FieldType.Number => One(Number(value, "#,0.############", settings)),

        // The number only. Which currency it is in is the author's to write, beside the tag.
        FieldType.Money => One(Number(value, "#,0.00", settings)),
        _ => null,
    };

    /// <summary>How to supply a value of this type, for the fix on a wrong-type diagnostic.</summary>
    public static string Expected(Field field) => $"Supply {field.Key} {Words[field.Type].Supply}.";

    /// <summary>What a value of this type is, for the message on a wrong-type diagnostic.</summary>
    public static string Described(FieldType type) => Words[type].Described;

    /// <summary>
    /// Text prints the way it was typed: a line break stays a line break, and a blank line starts a new paragraph.
    /// </summary>
    private static List<string[]>? Text(JsonElement value)
    {
        if (value.ValueKind != JsonValueKind.String)
        {
            return null;
        }

        var paragraphs = new List<string[]>();
        var lines = new List<string>();
        foreach (var line in value.GetString()!.Split(LineEndings, StringSplitOptions.None).Append(""))
        {
            if (!string.IsNullOrWhiteSpace(line))
            {
                lines.Add(line);
            }
            else if (lines.Count > 0)
            {
                paragraphs.Add([.. lines]);
                lines.Clear();
            }
        }

        return paragraphs.Count > 0 ? paragraphs : [[""]];
    }

    private static string[][]? One(string[]? lines) => lines is null ? null : [lines];

    private static string[]? Date(JsonElement value, RenderSettings settings) =>
        value.ValueKind == JsonValueKind.String && DateOnly.TryParseExact(
            value.GetString(), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date)
            ? [date.ToString("d MMMM yyyy", settings.Culture)]
            : null;

    private static string[]? Address(JsonElement value) =>
        value.ValueKind == JsonValueKind.Array && value.EnumerateArray().All(line => line.ValueKind == JsonValueKind.String)
            ? [.. value.EnumerateArray().Select(line => line.GetString()!)]
            : null;

    private static string[]? Number(JsonElement value, string format, RenderSettings settings) =>
        value.ValueKind == JsonValueKind.Number && value.TryGetDecimal(out var number)
            ? [number.ToString(format, settings.Culture)]
            : null;
}
