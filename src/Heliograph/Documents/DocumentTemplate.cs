using System.Xml;
using System.Xml.Linq;

namespace Heliograph.Documents;

/// <summary>The document part of a correspondence: a .docx an author wrote, with ((fields)) from the dictionary.</summary>
public static class DocumentTemplate
{
    /// <summary>
    /// Checks a .docx against the dictionary. A template that compiles can be rendered; one that does not comes
    /// back with every problem found, each with its place and a fix.
    /// </summary>
    public static DocumentCompilation Compile(Stream docx, FieldDictionary dictionary)
    {
        ArgumentNullException.ThrowIfNull(docx);
        ArgumentNullException.ThrowIfNull(dictionary);

        var package = DocxPackage.Read(docx, out var refusalCode);
        if (package is null)
        {
            return new DocumentCompilation(null, [refusalCode == DiagnosticCodes.TooLarge ? TooLarge() : NotAWordDocument()]);
        }

        try
        {
            if (DocxSafety.Check(package) is { Count: > 0 } refused)
            {
                return new DocumentCompilation(null, refused);
            }

            if (package.TextParts() is not { } parts)
            {
                return new DocumentCompilation(null, [NotAWordDocument()]);
            }

            var diagnostics = new List<Diagnostic>();
            var compiled = new Dictionary<string, XDocument>();
            foreach (var (name, label) in parts)
            {
                var xml = package.Xml(name)!;
                TagScanner.Compile(xml, label, dictionary, diagnostics);
                compiled[name] = xml;
            }

            if (AuthorDetails.Removed(package) is { } properties)
            {
                compiled[properties.Key] = properties.Value;
            }

            return diagnostics.Count == 0
                ? new DocumentCompilation(new CompiledDocument(package, compiled, dictionary), [])
                : new DocumentCompilation(null, diagnostics);
        }
        catch (XmlException)
        {
            // Malformed XML, or a document type definition, which a Word document never needs.
            return new DocumentCompilation(null, [NotAWordDocument()]);
        }
    }

    private static Diagnostic NotAWordDocument() => new(DiagnosticCodes.NotAWordDocument,
        "This file is not a Word document (.docx) that can be read.",
        "Open it in your word processor, save it as a Word document (.docx), and upload that.");

    private static Diagnostic TooLarge() => new(DiagnosticCodes.TooLarge,
        $"This file is too large to check safely. It unpacks to more than {DocxPackage.MaxUnpackedBytes / (1024 * 1024)} MB.",
        "Make the document smaller, for example by compressing its pictures.");
}

/// <summary>The result of compiling a template: either a document that can be rendered, or what is wrong with it.</summary>
public sealed class DocumentCompilation
{
    internal DocumentCompilation(CompiledDocument? document, IReadOnlyList<Diagnostic> diagnostics)
    {
        Document = document;
        Diagnostics = diagnostics;
    }

    /// <summary>The compiled template, or null when there are diagnostics.</summary>
    public CompiledDocument? Document { get; }

    public IReadOnlyList<Diagnostic> Diagnostics { get; }

    public bool Succeeded => Document is not null;
}
