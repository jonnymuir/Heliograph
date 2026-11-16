using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Xml;
using System.Xml.Linq;

namespace Heliograph.Documents;

/// <summary>Turns one value from the data into the lines of text that go in a slot, according to its type.</summary>
internal static class ValueWriter
{
    private static readonly string[] LineEndings = ["\r\n", "\n", "\r"];

    /// <summary>The lines to print, or null when the value is not of the field's type.</summary>
    public static IReadOnlyList<string>? Lines(Field field, JsonElement value, RenderSettings settings) => field.Type switch
    {
        FieldType.Text when value.ValueKind == JsonValueKind.String => value.GetString()!.Split(LineEndings, StringSplitOptions.None),
        FieldType.Date when value.ValueKind == JsonValueKind.String && DateOnly.TryParseExact(
                value.GetString(), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date)
            => [date.ToString("d MMMM yyyy", settings.Culture)],
        FieldType.Address when value.ValueKind == JsonValueKind.Array
                               && value.EnumerateArray().All(line => line.ValueKind == JsonValueKind.String)
            => [.. value.EnumerateArray().Select(line => line.GetString()!)],
        _ => null,
    };

    /// <summary>How to supply a value of this type, for the fix on a wrong-type diagnostic.</summary>
    public static string Expected(Field field) => field.Type switch
    {
        FieldType.Date => $"Supply {field.Key} as a date in the form 2027-06-19.",
        FieldType.Address => $"Supply {field.Key} as a list of address lines, each of them text.",
        _ => $"Supply {field.Key} as text.",
    };

    /// <summary>
    /// The elements that print the lines inside a run: text, with a line break between lines. The text is data and
    /// stays data: it is written as text content, never parsed.
    /// </summary>
    public static IEnumerable<XElement> Elements(IReadOnlyList<string> lines)
    {
        for (var index = 0; index < lines.Count; index++)
        {
            if (index > 0)
            {
                yield return new XElement(TagScanner.Word + "br");
            }

            if (Printable(lines[index]) is { Length: > 0 } text)
            {
                yield return new XElement(TagScanner.Word + "t", new XAttribute(XNamespace.Xml + "space", "preserve"), text);
            }
        }
    }

    /// <summary>Drops the characters XML cannot hold (most control characters), which would make the file unreadable.</summary>
    private static string Printable(string text)
    {
        var kept = new StringBuilder(text.Length);
        for (var index = 0; index < text.Length; index++)
        {
            if (index + 1 < text.Length && XmlConvert.IsXmlSurrogatePair(text[index + 1], text[index]))
            {
                kept.Append(text[index]).Append(text[++index]);
            }
            else if (XmlConvert.IsXmlChar(text[index]))
            {
                kept.Append(text[index]);
            }
        }

        return kept.ToString();
    }
}
