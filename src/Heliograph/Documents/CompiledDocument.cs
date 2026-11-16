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

        var recipient = new RecipientData(data, settings);
        var rendered = new Dictionary<string, XDocument>();
        foreach (var (name, template) in _parts)
        {
            // A copy, so the compiled template is never changed and can be shared.
            var part = new XDocument(template);
            ResolveConditions(part, recipient);
            foreach (var slot in part.Descendants(TagScanner.Slot + "value").ToList())
            {
                if (recipient.Paragraphs(_fields[(string)slot.Attribute("key")!]) is { } paragraphs)
                {
                    ValueWriter.Write(slot, paragraphs);
                }
            }

            rendered[name] = part;
        }

        return recipient.Diagnostics.Count == 0
            ? new DocumentRender(_package.Save(rendered), [])
            : new DocumentRender(null, recipient.Diagnostics);
    }

    /// <summary>
    /// Keeps or removes the content each condition governs, outermost first. It runs before any value is filled in,
    /// so a field used only in removed content needs no value.
    /// </summary>
    private void ResolveConditions(XDocument part, RecipientData recipient)
    {
        while (part.Descendants(Conditions.Element).FirstOrDefault() is { } condition)
        {
            if (recipient.IsYes(_fields[(string)condition.Attribute("key")!]))
            {
                condition.ReplaceWith(condition.Nodes());
            }
            else
            {
                condition.Remove();
            }
        }
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
