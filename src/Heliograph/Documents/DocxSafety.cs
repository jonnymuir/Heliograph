namespace Heliograph.Documents;

/// <summary>
/// Refuses a file that could run code or reach outside itself. An uploaded template is untrusted, and whatever it
/// carries would otherwise be copied into every letter rendered from it.
/// </summary>
internal static class DocxSafety
{
    public static IReadOnlyList<Diagnostic> Check(DocxPackage package)
    {
        var diagnostics = new List<Diagnostic>();
        var contentTypes = package.DeclaredContentTypes().Select(t => t.ContentType).ToList();
        var names = package.Names.ToList();

        if (contentTypes.Any(t => Mentions(t, "macroEnabled") || Mentions(t, "vbaProject"))
            || names.Any(n => Mentions(n, "vbaProject")))
        {
            diagnostics.Add(new Diagnostic(DiagnosticCodes.ContainsMacros,
                "This document contains macros, or is saved in a format that allows them.",
                "Save it as a Word document (.docx), which cannot hold macros, and upload that."));
        }

        if (contentTypes.Any(t => Mentions(t, "oleObject") || Mentions(t, "activeX"))
            || names.Any(n => Mentions(n, "/embeddings/") || Mentions(n, "/activeX/")))
        {
            diagnostics.Add(new Diagnostic(DiagnosticCodes.ContainsEmbeddedObject,
                "This document contains an embedded file or object, such as a spreadsheet or a control.",
                "Remove it, or paste it in as a picture or as plain text."));
        }

        // A link a reader clicks is harmless. Anything else outside the file is fetched when the document opens:
        // a remote picture, a template on a file share, a linked object.
        var outside = names.Where(n => n.EndsWith(".rels", StringComparison.OrdinalIgnoreCase))
            .SelectMany(package.RelationshipsIn)
            .FirstOrDefault(r => r.External && !r.Type.EndsWith("/hyperlink", StringComparison.Ordinal));
        if (outside.Target is not null)
        {
            diagnostics.Add(new Diagnostic(DiagnosticCodes.LoadsOutsideContent,
                $"This document loads content from outside the file: {Shorten(outside.Target)}.",
                "Put the content in the document itself. For a picture, insert the picture, not a link to it."));
        }

        return diagnostics;
    }

    private static bool Mentions(string text, string word) => text.Contains(word, StringComparison.OrdinalIgnoreCase);

    private static string Shorten(string target) => target.Length <= 80 ? target : target[..77] + "...";
}
