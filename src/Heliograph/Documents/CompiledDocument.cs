using System.Text.Json;
using System.Xml.Linq;

namespace Heliograph.Documents;

/// <summary>A template that has passed every check. It can be rendered any number of times, from any thread.</summary>
public sealed class CompiledDocument
{
    private readonly DocxPackage _package;
    private readonly IReadOnlyDictionary<string, XDocument> _parts;
    private readonly Dictionary<string, Field> _fields;

    internal CompiledDocument(DocxPackage package, IReadOnlyDictionary<string, XDocument> parts, FieldDictionary dictionary)
    {
        _package = package;
        _parts = parts;
        _fields = dictionary.Fields.ToDictionary(field => field.Key);
    }

    /// <summary>
    /// Fills the template in for one recipient. The data is a JSON object keyed by the dictionary's keys. When
    /// the data is wrong, no document is produced and every problem is reported.
    /// </summary>
    public DocumentRender Render(JsonElement data, RenderSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        var diagnostics = new Dictionary<string, Diagnostic>();
        var rendered = new Dictionary<string, XDocument>();
        foreach (var (name, template) in _parts)
        {
            // A copy, so the compiled template is never changed and can be shared.
            var part = new XDocument(template);
            foreach (var slot in part.Descendants(TagScanner.Slot + "value").ToList())
            {
                var field = _fields[(string)slot.Attribute("key")!];
                if (Lines(field, data, settings, diagnostics) is { } lines)
                {
                    slot.ReplaceWith(ValueWriter.Elements(lines));
                }
            }

            rendered[name] = part;
        }

        return diagnostics.Count == 0
            ? new DocumentRender(_package.Save(rendered), [])
            : new DocumentRender(null, [.. diagnostics.Values]);
    }

    private static IReadOnlyList<string>? Lines(
        Field field, JsonElement data, RenderSettings settings, Dictionary<string, Diagnostic> diagnostics)
    {
        if (data.ValueKind != JsonValueKind.Object || !data.TryGetProperty(field.Key, out var value)
                                                   || value.ValueKind == JsonValueKind.Null)
        {
            diagnostics.TryAdd(field.Key, new Diagnostic(DiagnosticCodes.ValueMissing,
                $"The data has no value for \"{field.Name}\".", $"Supply {field.Key} in the data."));
            return null;
        }

        var lines = ValueWriter.Lines(field, value, settings);
        if (lines is null)
        {
            diagnostics.TryAdd(field.Key, new Diagnostic(DiagnosticCodes.ValueWrongType,
                $"The value for \"{field.Name}\" is not {Described(field.Type)}.", ValueWriter.Expected(field)));
        }

        return lines;
    }

    private static string Described(FieldType type) => type switch
    {
        FieldType.Date => "a date",
        FieldType.Address => "an address",
        _ => "text",
    };
}

/// <summary>The result of a render: a finished .docx, or what is wrong with the data.</summary>
public sealed class DocumentRender
{
    internal DocumentRender(byte[]? docx, IReadOnlyList<Diagnostic> diagnostics)
    {
        Docx = docx;
        Diagnostics = diagnostics;
    }

    /// <summary>The finished document, or null when there are diagnostics.</summary>
#pragma warning disable CA1819 // The rendered file is a block of bytes; a copy per read would only cost memory.
    public byte[]? Docx { get; }
#pragma warning restore CA1819

    public IReadOnlyList<Diagnostic> Diagnostics { get; }
}
