namespace Heliograph.Documents;

/// <summary>
/// Refuses a file that could run code or reach outside itself. An uploaded template is untrusted, and whatever it
/// carries would otherwise be copied into every letter rendered from it.
/// </summary>
internal static class DocxSafety
{
    public static IReadOnlyList<Diagnostic> Check(DocxPackage package)
    {
        var contentTypes = package.DeclaredContentTypes().Select(t => t.ContentType).ToList();
        var names = package.Names.ToList();
        Diagnostic?[] found = [Macros(contentTypes, names), EmbeddedObject(contentTypes, names), OutsideContent(package, names), Signature(names)];
        return [.. found.OfType<Diagnostic>()];
    }

    private static Diagnostic? Macros(List<string> contentTypes, List<string> names) =>
        contentTypes.Any(t => Mentions(t, "macroEnabled") || Mentions(t, "vbaProject")) || names.Any(n => Mentions(n, "vbaProject"))
            ? new Diagnostic(DiagnosticCodes.ContainsMacros,
                "This document contains macros, or is saved in a format that allows them.",
                "Save it as a Word document (.docx), which cannot hold macros, and upload that.")
            : null;

    private static Diagnostic? EmbeddedObject(List<string> contentTypes, List<string> names) =>
        contentTypes.Any(t => Mentions(t, "oleObject") || Mentions(t, "activeX"))
        || names.Any(n => Mentions(n, "/embeddings/") || Mentions(n, "/activeX/"))
            ? new Diagnostic(DiagnosticCodes.ContainsEmbeddedObject,
                "This document contains an embedded file or object, such as a spreadsheet or a control.",
                "Remove it, or paste it in as a picture or as plain text.")
            : null;

    /// <summary>
    /// A link a reader clicks is harmless. Anything else outside the file is fetched when the document opens: a
    /// remote picture, a template on a file share, a linked object.
    /// </summary>
    private static Diagnostic? OutsideContent(DocxPackage package, List<string> names)
    {
        var outside = names.Where(n => n.EndsWith(".rels", StringComparison.OrdinalIgnoreCase))
            .SelectMany(package.RelationshipsIn)
            .FirstOrDefault(r => r.External && !r.Type.EndsWith("/hyperlink", StringComparison.Ordinal));
        return outside.Target is null
            ? null
            : new Diagnostic(DiagnosticCodes.LoadsOutsideContent,
                $"This document loads content from outside the file: {Shorten(outside.Target)}.",
                "Put the content in the document itself. For a picture, insert the picture, not a link to it.");
    }

    private static Diagnostic? Signature(List<string> names) =>
        names.Any(n => n.StartsWith("_xmlsignatures/", StringComparison.OrdinalIgnoreCase))
            ? new Diagnostic(DiagnosticCodes.IsSigned,
                "This document is digitally signed. Filling it in would break the signature on every letter.",
                "Remove the signature, save, and upload it again.")
            : null;

    private static bool Mentions(string text, string word) => text.Contains(word, StringComparison.OrdinalIgnoreCase);

    private static string Shorten(string target) => target.Length <= 80 ? target : target[..77] + "...";
}
