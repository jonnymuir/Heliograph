using System.Text.Json;
using System.Xml.Linq;

namespace Heliograph.Documents;

/// <summary>A template that has passed every check. It can be rendered any number of times, from any thread.</summary>
public sealed class CompiledDocument
{
    private readonly DocxPackage _package;
    private readonly IReadOnlyDictionary<string, XDocument> _parts;
    private readonly FieldDictionary _dictionary;
    private readonly Dictionary<string, Field> _fields;

    internal CompiledDocument(DocxPackage package, IReadOnlyDictionary<string, XDocument> parts, FieldDictionary dictionary)
    {
        _package = package;
        _parts = parts;
        _dictionary = dictionary;
        _fields = dictionary.Fields.ToDictionary(field => field.Key);
    }

    /// <summary>The dictionary the template was compiled against.</summary>
    internal FieldDictionary Dictionary => _dictionary;

    /// <summary>The yes or no fields the template's conditions depend on, each once.</summary>
    internal IReadOnlyList<Field> ConditionFields => [.. _parts.Values
        .SelectMany(part => part.Descendants(Conditions.Element))
        .Select(FieldOf)
        .Distinct()];

    /// <summary>
    /// Fills the template in for one recipient. The data is a JSON object keyed by the dictionary's keys. When
    /// the data is wrong, no document is produced and every problem is reported.
    /// </summary>
    public DocumentRender Render(JsonElement data, RenderSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        var recipient = new RecipientData(data, settings, _dictionary);
        var rendered = RenderParts(recipient);
        return recipient.Diagnostics.Count == 0
            ? new DocumentRender(_package.Save(rendered), [])
            : new DocumentRender(null, recipient.Diagnostics);
    }

    /// <summary>Each part filled in for one recipient, before it is packed into a file.</summary>
    internal Dictionary<string, XDocument> RenderParts(RecipientData recipient)
    {
        var rendered = new Dictionary<string, XDocument>();
        foreach (var (name, template) in _parts)
        {
            // A copy, so the compiled template is never changed and can be shared.
            var part = new XDocument(template);
            bool IsYes(XElement condition) => recipient.IsYes(FieldOf(condition), condition);

            // Conditions outside repeated rows go first, so that a list inside hidden content needs no value.
            ConditionResolver.Resolve(part, IsYes, insideRepeatedRows: false);
            Repeats.Expand(part, key => _fields[key], recipient);
            ConditionResolver.Resolve(part, IsYes, insideRepeatedRows: true);
            DocumentMender.Mend(part);
            foreach (var slot in part.Descendants(TagScanner.Value).ToList())
            {
                if (recipient.Paragraphs(FieldOf(slot), slot) is { } paragraphs)
                {
                    ValueWriter.Write(slot, paragraphs);
                }
            }

            rendered[name] = part;
        }

        return rendered;
    }

    /// <summary>The field a stand-in is for. One that belongs to a list is found through the list, where its key is unique.</summary>
    private Field FieldOf(XElement standIn)
    {
        var key = (string)standIn.Attribute("key")!;
        return (string?)standIn.Attribute("list") is { } list ? _fields[list].Fields!.First(field => field.Key == key) : _fields[key];
    }
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
