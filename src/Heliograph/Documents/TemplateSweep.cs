using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace Heliograph.Documents;

/// <summary>
/// Leaves no tag behind. After the text has been compiled, every part of the file is searched, text and attributes
/// alike, for a tag that was not processed: one in a picture's description, a field's code, a link's address, a
/// chart. Left alone it would be printed, or hidden, exactly as typed.
/// </summary>
internal static partial class TemplateSweep
{
    private static readonly XName Text = WordXml.Word + "t";
    private static readonly XName Paragraph = WordXml.Word + "p";

    /// <summary>What the sweep finds. <paramref name="textParts"/> names the parts whose paragraphs were compiled.</summary>
    public static IEnumerable<Diagnostic> Run(DocxPackage package, IReadOnlyDictionary<string, XDocument> compiled,
        ISet<string> textParts, FieldDictionary dictionary)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var name in package.Names.Where(IsXml))
        {
            var document = compiled.GetValueOrDefault(name) ?? package.Xml(name)!;
            var tags = Strings(document, textParts.Contains(name)).SelectMany(text => Tag().Matches(text)).Select(m => m.Groups[1].Value);
            foreach (var tag in tags.Where(tag => seen.Add(FieldName.Normalise(tag))))
            {
                yield return Report(tag, Place(name), dictionary);
            }
        }
    }

    private static bool IsXml(string name) =>
        name.EndsWith(".xml", StringComparison.OrdinalIgnoreCase) || name.EndsWith(".rels", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Everything in a part that could hold a tag and has not been compiled. In a part whose paragraphs were
    /// compiled that is the attributes and the text outside ordinary text runs, such as a field's code.
    /// </summary>
    private static IEnumerable<string> Strings(XDocument document, bool compiled)
    {
        var attributes = document.Descendants().Attributes().Select(attribute => attribute.Value);
        var loose = document.DescendantNodes().OfType<XText>().Where(text => text.Parent?.Name != Text).Select(text => text.Value);
        var paragraphs = compiled
            ? []
            : document.Descendants(Paragraph).Select(p => string.Concat(p.Descendants(Text).Select(t => t.Value)));
        return attributes.Concat(loose).Concat(paragraphs);
    }

    private static Diagnostic Report(string tag, string place, FieldDictionary dictionary)
    {
        var normalised = FieldName.Normalise(tag);
        var name = normalised.StartsWith("if ", StringComparison.Ordinal) ? tag.Trim()[2..] : tag;
        if (normalised == "end if" || dictionary.Find(name) is not null)
        {
            return new Diagnostic(DiagnosticCodes.TagNotFilledIn,
                $"There is a tag, (({tag.Trim()})), in {place}, where tags are not filled in.",
                "Move it into the text of the document, or remove it.");
        }

        return new Diagnostic(DiagnosticCodes.TagNotFilledIn,
            $"The text (({tag.Trim()})) in {place} looks like a tag. It names no field, and tags are not filled in there.",
            "If it was meant as a tag, move it into the text of the document. If not, nothing needs to change.",
            Severity: DiagnosticSeverity.Warning);
    }

    private static string Place(string partName) => partName switch
    {
        _ when partName.EndsWith(".rels", StringComparison.OrdinalIgnoreCase) => "the address of a link",
        _ when partName.StartsWith("docProps/", StringComparison.OrdinalIgnoreCase) => "the document's properties, such as its title",
        _ when partName.Contains("/charts/", StringComparison.OrdinalIgnoreCase) => "a chart",
        _ when partName.Contains("/diagrams/", StringComparison.OrdinalIgnoreCase) => "a diagram",
        _ when partName.Contains("/glossary/", StringComparison.OrdinalIgnoreCase) => "the document's building blocks",
        _ => "a place that is not ordinary text, such as a picture's description or a field's code",
    };

    [GeneratedRegex(@"\(\(\s*([^()]{1,100}?)\s*\)\)", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex Tag();
}
