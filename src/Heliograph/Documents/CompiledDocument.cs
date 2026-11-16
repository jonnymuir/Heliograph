using System.Text.Json;
using System.Xml.Linq;

namespace Heliograph.Documents;

/// <summary>A template that has passed every check. It can be rendered any number of times, from any thread.</summary>
public sealed class CompiledDocument
{
    private readonly DocxPackage _package;
    private readonly IReadOnlyDictionary<string, XDocument> _parts;
    private readonly Dictionary<string, DictionaryEntry> _entries;

    internal CompiledDocument(DocxPackage package, IReadOnlyDictionary<string, XDocument> parts, DataDictionary dictionary)
    {
        _package = package;
        _parts = parts;
        _entries = dictionary.Entries.ToDictionary(entry => entry.Key);
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
            foreach (var slot in part.Descendants(FieldScanner.Slot + "value").ToList())
            {
                var entry = _entries[(string)slot.Attribute("key")!];
                if (Lines(entry, data, settings, diagnostics) is { } lines)
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
        DictionaryEntry entry, JsonElement data, RenderSettings settings, Dictionary<string, Diagnostic> diagnostics)
    {
        if (data.ValueKind != JsonValueKind.Object || !data.TryGetProperty(entry.Key, out var value)
                                                   || value.ValueKind == JsonValueKind.Null)
        {
            diagnostics.TryAdd(entry.Key, new Diagnostic(DiagnosticCodes.ValueMissing,
                $"The data has no value for \"{entry.Name}\".", $"Supply {entry.Key} in the data."));
            return null;
        }

        var lines = ValueWriter.Lines(entry, value, settings);
        if (lines is null)
        {
            diagnostics.TryAdd(entry.Key, new Diagnostic(DiagnosticCodes.ValueWrongType,
                $"The value for \"{entry.Name}\" is not {Described(entry.Type)}.", ValueWriter.Expected(entry)));
        }

        return lines;
    }

    private static string Described(EntryType type) => type switch
    {
        EntryType.Date => "a date",
        EntryType.Address => "an address",
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
