using System.Xml.Linq;

namespace Heliograph.Documents;

/// <summary>
/// The comments Heliograph puts in an author's own document, one at each problem, so they can fix it where they
/// work. When the document comes back, these comments are taken out before it is checked. Anyone else's comments are
/// still refused, because those are an author's remarks that would reach every recipient.
/// </summary>
public static class HeliographComments
{
    /// <summary>The author name every Heliograph comment carries.</summary>
    public const string Author = "Heliograph";

    private const string CommentsType = "http://schemas.openxmlformats.org/officeDocument/2006/relationships/comments";
    private const string CommentsContentType = "application/vnd.openxmlformats-officedocument.wordprocessingml.comments+xml";
    private static readonly XNamespace Word = WordXml.Word;
    private static readonly XNamespace Relationships = "http://schemas.openxmlformats.org/package/2006/relationships";
    private static readonly XNamespace ContentTypes = "http://schemas.openxmlformats.org/package/2006/content-types";
    private static readonly HashSet<string> Marks = ["commentRangeStart", "commentRangeEnd", "commentReference"];

    /// <summary>
    /// The author's document with a comment at each problem in its document part, or null when the file cannot be
    /// read as a Word document. A problem with no paragraph of its own, or in a header, footer or note, is commented
    /// on the first paragraph and says where it is. Any earlier Heliograph comments are replaced.
    /// </summary>
    public static byte[]? Annotate(byte[] docx, IEnumerable<Diagnostic> diagnostics)
    {
        ArgumentNullException.ThrowIfNull(docx);
        ArgumentNullException.ThrowIfNull(diagnostics);
        if (DocxPackage.Read(new MemoryStream(docx, writable: false), out _) is not { } read || Strip(read) is not { } package
            || package.TextParts() is not { } parts)
        {
            return null;
        }

        var main = parts[0].Name;
        var document = package.Xml(main)!;
        var paragraphs = document.Descendants(Word + "p").ToList();
        if (paragraphs.Count == 0)
        {
            return null;
        }

        var folder = main.Contains('/', StringComparison.Ordinal) ? main[..(main.LastIndexOf('/') + 1)] : "";
        var commentsName = folder + "comments.xml";
        var comments = package.Xml(commentsName) ?? new XDocument(new XElement(Word + "comments", new XAttribute(XNamespace.Xmlns + "w", Word)));
        var id = comments.Descendants(Word + "comment").Select(c => (int?)c.Attribute(Word + "id") ?? 0).DefaultIfEmpty(-1).Max() + 1;
        foreach (var diagnostic in diagnostics)
        {
            var (paragraph, prefix) = diagnostic.Location is { Part: "document" } location && location.Paragraph <= paragraphs.Count
                ? (paragraphs[location.Paragraph - 1], "")
                : (paragraphs[0], diagnostic.Location is { } elsewhere ? $"In the {elsewhere.Part}, paragraph {elsewhere.Paragraph} (\"{elsewhere.Excerpt}\"): " : "");
            Mark(paragraph, id);
            comments.Root!.Add(new XElement(Word + "comment",
                new XAttribute(Word + "id", id), new XAttribute(Word + "author", Author), new XAttribute(Word + "initials", "H"),
                new XElement(Word + "p", new XElement(Word + "r", new XElement(Word + "t", new XAttribute(XNamespace.Xml + "space", "preserve"),
                    $"{prefix}{diagnostic.Message} {diagnostic.Fix} ({diagnostic.Code})")))));
            id++;
        }

        var added = new Dictionary<string, XDocument> { [commentsName] = comments };
        var replaced = new Dictionary<string, XDocument> { [main] = document };
        if (!package.Has(commentsName))
        {
            replaced[$"{folder}_rels/{main[folder.Length..]}.rels"] = WithRelationship(package.Xml($"{folder}_rels/{main[folder.Length..]}.rels"));
            replaced["[Content_Types].xml"] = WithContentType(package.Xml("[Content_Types].xml")!, "/" + commentsName);
        }

        return package.Save(replaced, added);
    }

    /// <summary>The package without Heliograph's comments, or the same package when it has none.</summary>
    internal static DocxPackage? Strip(DocxPackage package)
    {
        var replaced = new Dictionary<string, XDocument>();
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var name in package.Names.Where(n => n.EndsWith("/comments.xml", StringComparison.OrdinalIgnoreCase)))
        {
            var comments = package.Xml(name)!;
            var ours = comments.Descendants(Word + "comment").Where(c => (string?)c.Attribute(Word + "author") == Author).ToList();
            if (ours.Count == 0)
            {
                continue;
            }

            ours.ForEach(c => ids.Add((string?)c.Attribute(Word + "id") ?? ""));
            ours.ForEach(c => c.Remove());
            replaced[name] = comments;
        }

        if (ids.Count == 0)
        {
            return package;
        }

        foreach (var (name, _) in package.TextParts() ?? [])
        {
            if (WithoutMarks(package.Xml(name)!, ids) is { } part)
            {
                replaced[name] = part;
            }
        }

        return DocxPackage.Read(new MemoryStream(package.Save(replaced)), out _);
    }

    /// <summary>The part without the marks of the given comments, or null when it had none.</summary>
    private static XDocument? WithoutMarks(XDocument part, HashSet<string> ids)
    {
        var marks = part.Descendants()
            .Where(e => e.Name.Namespace == Word && Marks.Contains(e.Name.LocalName) && ids.Contains((string?)e.Attribute(Word + "id") ?? ""))
            .ToList();
        foreach (var mark in marks)
        {
            // A run that held only the reference goes with it.
            var run = mark.Parent is { } parent && parent.Name == Word + "r" && parent.Elements().All(e => e == mark || e.Name == Word + "rPr") ? parent : null;
            (run ?? mark).Remove();
        }

        return marks.Count > 0 ? part : null;
    }

    private static void Mark(XElement paragraph, int id)
    {
        var start = new XElement(Word + "commentRangeStart", new XAttribute(Word + "id", id));
        if (paragraph.Element(Word + "pPr") is { } properties)
        {
            properties.AddAfterSelf(start);
        }
        else
        {
            paragraph.AddFirst(start);
        }

        paragraph.Add(
            new XElement(Word + "commentRangeEnd", new XAttribute(Word + "id", id)),
            new XElement(Word + "r", new XElement(Word + "commentReference", new XAttribute(Word + "id", id))));
    }

    private static XDocument WithRelationship(XDocument? relationships)
    {
        relationships ??= new XDocument(new XElement(Relationships + "Relationships"));
        var taken = relationships.Root!.Elements().Select(r => (string?)r.Attribute("Id")).ToHashSet();
        var number = 1;
        while (taken.Contains($"rIdHeliograph{number}"))
        {
            number++;
        }

        relationships.Root.Add(new XElement(Relationships + "Relationship",
            new XAttribute("Id", $"rIdHeliograph{number}"), new XAttribute("Type", CommentsType), new XAttribute("Target", "comments.xml")));
        return relationships;
    }

    private static XDocument WithContentType(XDocument types, string partName)
    {
        types.Root!.Add(new XElement(ContentTypes + "Override", new XAttribute("PartName", partName), new XAttribute("ContentType", CommentsContentType)));
        return types;
    }
}
