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

        var package = DocxPackage.Read(docx, out var refusal);
        if (package is null)
        {
            return new DocumentCompilation(null, [Refused(refusal)]);
        }

        try
        {
            if (DocxSafety.Check(package) is { Count: > 0 } unsafeContent)
            {
                return new DocumentCompilation(null, unsafeContent);
            }

            if (package.TextParts() is not { } parts)
            {
                return new DocumentCompilation(null, [NotAWordDocument()]);
            }

            return TemplateFitness.Check(package, parts) is { Count: > 0 } unfit
                ? new DocumentCompilation(null, unfit)
                : CompileParts(package, parts, dictionary);
        }
        catch (XmlException)
        {
            // Malformed XML, or a document type definition, which a Word document never needs.
            return new DocumentCompilation(null, [NotAWordDocument()]);
        }
    }

    private static DocumentCompilation CompileParts(
        DocxPackage package, IReadOnlyList<(string Name, string Label)> parts, FieldDictionary dictionary)
    {
        var diagnostics = new List<Diagnostic>();
        var compiled = new Dictionary<string, XDocument>();
        var nearMiss = new NearMiss(dictionary);
        foreach (var (name, label) in parts)
        {
            var xml = package.Xml(name)!;
            TagScanner.Compile(xml, new PartScanSettings(label, dictionary, nearMiss), diagnostics);
            compiled[name] = xml;
        }

        foreach (var (name, xml) in AuthorDetails.Removed(package))
        {
            compiled[name] = xml;
        }

        diagnostics.AddRange(TemplateSweep.Run(package, compiled, parts.Select(part => part.Name).ToHashSet(), dictionary));
        if (AuthorDetails.Title(package) is null)
        {
            diagnostics.Add(NoTitle());
        }

        if (diagnostics.Errors() > 0)
        {
            return new DocumentCompilation(null, diagnostics);
        }

        var document = new CompiledDocument(package, compiled, dictionary);
        return Rehearsal.Run(document) is { } broken
            ? new DocumentCompilation(null, [.. diagnostics, broken])
            : new DocumentCompilation(document, diagnostics);
    }

    private static Diagnostic Refused(DocxRefusal? refusal) => refusal switch
    {
        DocxRefusal.TooLarge => TooLarge(),
        DocxRefusal.OldOrProtected => new Diagnostic(DiagnosticCodes.NotAWordDocument,
            "This file is an older Word document (.doc), or is protected with a password, so it cannot be read.",
            "Open it in your word processor, remove any password, save it as a Word document (.docx), and upload that."),
        _ => NotAWordDocument(),
    };

    private static Diagnostic NotAWordDocument() => new(DiagnosticCodes.NotAWordDocument,
        "This file is not a Word document (.docx) that can be read.",
        "Open it in your word processor, save it as a Word document (.docx), and upload that.");

    private static Diagnostic NoTitle() => new(DiagnosticCodes.DocumentHasNoTitle,
        "This document has no title in its properties. A PDF made from it will not meet the standard for accessible PDF, "
        + "because a screen reader announces the title first.",
        "Set a title that says what the letter is, such as \"Your annual statement\": File > Properties > Description in "
        + "LibreOffice, File > Info in Word. Then save and upload it again.",
        Severity: DiagnosticSeverity.Warning);

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

    /// <summary>The compiled template, or null when any diagnostic is an error.</summary>
    public CompiledDocument? Document { get; }

    /// <summary>Everything found. A template that compiled may still carry warnings for its author to look at.</summary>
    public IReadOnlyList<Diagnostic> Diagnostics { get; }

    public bool Succeeded => Document is not null;
}
