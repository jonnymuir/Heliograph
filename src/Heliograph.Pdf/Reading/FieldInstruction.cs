using System.Text.RegularExpressions;

namespace Heliograph.Pdf.Reading;

/// <summary>
/// A field's instruction read: its name, the number format a switch gives it (figures, letters or Roman numerals, as
/// Word writes them, case and all), or what in it the converter does not work out. Formatting switches that only say to
/// keep the result's look are passed over.
/// </summary>
internal sealed partial record FieldInstruction(string Name, string? Format, string? Unsupported)
{
    public static FieldInstruction Parse(string? instruction)
    {
        var tokens = Token().Matches(instruction ?? "").Select(m => m.Value).ToList();
        if (tokens.Count == 0)
        {
            return new("", null, null);
        }

        var (format, unsupported) = ((string?)null, (string?)null);
        for (var i = 1; i < tokens.Count; i++)
        {
            var argument = i + 1 < tokens.Count ? tokens[i + 1] : "";
            var (read, skip) = tokens[i] == "\\*" ? (Switch(argument), 1) : ((Kind: "unsupported", Value: (string?)tokens[i]), 0);
            (format, unsupported) = read.Kind switch
            {
                "format" => (read.Value, unsupported),
                "ignored" => (format, unsupported),
                _ => (format, unsupported ?? (tokens[i] + (skip > 0 ? " " + argument : ""))),
            };
            i += skip;
        }

        return new(tokens[0].ToUpperInvariant(), format, unsupported);
    }

    // A \* switch: a number format, a switch that only keeps the result's look, or one the converter does not work out.
    private static (string Kind, string? Value) Switch(string argument) => argument switch
    {
        "roman" => ("format", "lowerRoman"),
        "ROMAN" => ("format", "upperRoman"),
        "alphabetic" => ("format", "lowerLetter"),
        "ALPHABETIC" => ("format", "upperLetter"),
        _ when argument.Equals("Arabic", StringComparison.OrdinalIgnoreCase) => ("format", "decimal"),
        _ when argument.Equals("MERGEFORMAT", StringComparison.OrdinalIgnoreCase) || argument.Equals("CHARFORMAT", StringComparison.OrdinalIgnoreCase) => ("ignored", null),
        _ => ("unsupported", argument),
    };

    [GeneratedRegex("\"[^\"]*\"|\\S+")]
    private static partial Regex Token();
}
