using System.Xml.Linq;

namespace Heliograph.Documents;

/// <summary>
/// Refuses a document that cannot be honoured as a template: one in a form the engine would misread, or one that
/// carries edits and remarks that would be copied to every recipient.
/// </summary>
internal static class TemplateFitness
{
    private const string StrictNamespace = "http://purl.oclc.org/ooxml/wordprocessingml/main";
    private static readonly XNamespace Word = WordXml.Word;
    private static readonly HashSet<string> TrackedChange = ["ins", "del", "moveFrom", "moveTo"];
    private static readonly HashSet<string> CommentMark = ["commentReference", "commentRangeStart", "commentRangeEnd"];

    public static IReadOnlyList<Diagnostic> Check(DocxPackage package, IReadOnlyList<(string Name, string Label)> parts)
    {
        var documents = parts.Select(part => package.Xml(part.Name)!).ToList();
        if (documents[0].Root?.Name.NamespaceName == StrictNamespace)
        {
            // Strict uses different names for every element, so nothing would be found and nothing filled in.
            return [new Diagnostic(DiagnosticCodes.StrictFormat,
                "This document is saved as \"Strict Open XML Document\", which cannot be used as a template.",
                "Save it again, choosing \"Word Document (.docx)\" as the file type, and upload that.")];
        }

        var diagnostics = new List<Diagnostic>();
        var elements = documents.SelectMany(document => document.Descendants()).Where(e => e.Name.Namespace == Word).ToList();
        if (elements.Any(e => TrackedChange.Contains(e.Name.LocalName) || e.Name.LocalName.EndsWith("Change", StringComparison.Ordinal)))
        {
            diagnostics.Add(new Diagnostic(DiagnosticCodes.HasTrackedChanges,
                "This document has tracked changes that have not been accepted or rejected. "
                + "They would be sent to every recipient, deleted text included.",
                "Accept or reject every change, turn off change tracking, save, and upload it again."));
        }

        if (elements.Any(e => CommentMark.Contains(e.Name.LocalName)) || HasCommentsPart(package))
        {
            diagnostics.Add(new Diagnostic(DiagnosticCodes.HasComments,
                "This document has comments in it. They would be sent to every recipient.",
                "Delete every comment, save, and upload it again."));
        }

        return diagnostics;
    }

    private static bool HasCommentsPart(DocxPackage package) => package.Names
        .Where(name => name.EndsWith("/comments.xml", StringComparison.OrdinalIgnoreCase))
        .Any(name => package.Xml(name)!.Descendants(Word + "comment").Any());
}
