using System.Xml.Linq;

namespace Heliograph.Documents;

/// <summary>
/// A check a host adds to compiling a document, for what only it knows: what its PDF route can lay out, say. Its
/// diagnostics are reported with the compiler's own, and an error stops the template as theirs do. Rendering adds no
/// layout of its own, so what a template contains is what every letter made from it contains.
/// </summary>
public interface IDocumentCheck
{
    /// <summary>What this check finds wrong with the template. Each has a stable code, a place and a fix.</summary>
    IReadOnlyList<Diagnostic> Check(DocumentToCheck document);
}

/// <summary>A template's parts as the compiler read them: the document, then its headers, footers and notes.</summary>
public sealed class DocumentToCheck
{
    internal DocumentToCheck(IReadOnlyList<DocumentPart> parts, XDocument? settings, XDocument? styles = null, XDocument? numbering = null)
    {
        Parts = parts;
        Settings = settings;
        Styles = styles;
        Numbering = numbering;
    }

    public IReadOnlyList<DocumentPart> Parts { get; }

    /// <summary>The document's settings part (word/settings.xml), when it has one.</summary>
    public XDocument? Settings { get; }

    /// <summary>The document's styles part (word/styles.xml), when it has one.</summary>
    public XDocument? Styles { get; }

    /// <summary>The document's list definitions (word/numbering.xml), when it has them.</summary>
    public XDocument? Numbering { get; }
}

/// <summary>One part of a template, and how to name a place in it the way the compiler does.</summary>
public sealed class DocumentPart
{
    private const int ExcerptLength = 80;
    private readonly List<XElement> _paragraphs;

    internal DocumentPart(string label, XDocument xml)
    {
        Label = label;
        Xml = xml;
        _paragraphs = [.. xml.Descendants(WordXml.Word + "p")];
    }

    /// <summary>"document", "header", "footer", "footnotes" or "endnotes".</summary>
    public string Label { get; }

    /// <summary>The part's XML, a copy the check may read but whose changes go nowhere.</summary>
    public XDocument Xml { get; }

    /// <summary>
    /// The place of an element: this part, the number of the paragraph that holds it (or, for something outside a
    /// paragraph, the first paragraph in the cell or table around it), and the start of that paragraph's text.
    /// </summary>
    public DiagnosticLocation Locate(XElement element)
    {
        ArgumentNullException.ThrowIfNull(element);
        var p = WordXml.Word + "p";
        // The paragraph it is in; else the first in it, or in the cell or table around it; else the last before it.
        var paragraph = element.AncestorsAndSelf(p).FirstOrDefault()
            ?? element.AncestorsAndSelf().TakeWhile(e => e.Name != WordXml.Word + "body" && e.Parent is not null)
                .Select(e => e.Descendants(p).FirstOrDefault()).FirstOrDefault(found => found is not null)
            ?? element.ElementsBeforeSelf().SelectMany(e => e.DescendantsAndSelf(p)).LastOrDefault();
        var number = paragraph is null ? 0 : _paragraphs.IndexOf(paragraph) + 1;
        var text = paragraph is null
            ? ""
            : string.Concat(paragraph.Descendants(WordXml.Word + "t").Where(t => t.Ancestors(WordXml.Word + "p").First() == paragraph).Select(t => t.Value));
        return new DiagnosticLocation(Label, number, text.Length <= ExcerptLength ? text : text[..ExcerptLength] + "...");
    }
}
