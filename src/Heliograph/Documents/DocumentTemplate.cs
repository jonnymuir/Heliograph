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
    public static DocumentCompilation Compile(Stream docx, FieldDictionary dictionary) => Compile(docx, dictionary, []);

    /// <summary>
    /// Checks a .docx against the dictionary and against the host's own checks, such as what its PDF route can lay out.
    /// </summary>
    public static DocumentCompilation Compile(Stream docx, FieldDictionary dictionary, IReadOnlyList<IDocumentCheck> checks)
    {
        ArgumentNullException.ThrowIfNull(checks);
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

            // The comments Heliograph put at each problem come out before anything is checked.
            if (HeliographComments.Strip(package) is not { } stripped)
            {
                return new DocumentCompilation(null, [NotAWordDocument()]);
            }

            package = stripped;
            if (package.TextParts() is not { } parts)
            {
                return new DocumentCompilation(null, [NotAWordDocument()]);
            }

            if (TagSheetLeftIn(package, parts) is { } tagSheet)
            {
                return new DocumentCompilation(null, [tagSheet]);
            }

            return TemplateFitness.Check(package, parts) is { Count: > 0 } unfit
                ? new DocumentCompilation(null, unfit)
                : CompileParts(package, parts, dictionary, checks);
        }
        catch (XmlException)
        {
            // Malformed XML, or a document type definition, which a Word document never needs.
            return new DocumentCompilation(null, [NotAWordDocument()]);
        }
    }

    private static DocumentCompilation CompileParts(
        DocxPackage package, IReadOnlyList<(string Name, string Label)> parts, FieldDictionary dictionary, IReadOnlyList<IDocumentCheck> checks)
    {
        var diagnostics = HostChecks(package, parts, checks);
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

        diagnostics.AddRange(PictureDescriptions.Missing(package, parts));

        if (diagnostics.Errors() > 0)
        {
            return new DocumentCompilation(null, diagnostics);
        }

        var document = new CompiledDocument(package, compiled, dictionary);
        return Rehearsal.Run(document) is { } broken
            ? new DocumentCompilation(null, [.. diagnostics, broken])
            : new DocumentCompilation(document, diagnostics);
    }

    // The host's checks read the parts as the author saved them, before tags are worked through.
    private static List<Diagnostic> HostChecks(DocxPackage package, IReadOnlyList<(string Name, string Label)> parts, IReadOnlyList<IDocumentCheck> checks)
    {
        if (checks.Count == 0)
        {
            return [];
        }

        var document = new DocumentToCheck(
            [.. parts.Select(part => new DocumentPart(part.Label, package.Xml(part.Name)!, id => package.Target(part.Name, id) is { } target ? package.Bytes(target) : null))],
            package.Xml("word/settings.xml"),
            package.Xml("word/styles.xml"), package.Xml("word/numbering.xml"));
        return [.. checks.SelectMany(check => check.Check(document))];
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

    /// <summary>
    /// The starter document's tag sheet, still in the template. Its tags would be filled in and sent to every
    /// recipient, and they would set off a dozen other diagnostics, so this is reported on its own.
    /// </summary>
    private static Diagnostic? TagSheetLeftIn(DocxPackage package, IReadOnlyList<(string Name, string Label)> parts)
    {
        var paragraphs = package.Xml(parts[0].Name)!.Descendants(WordXml.Word + "p").ToList();
        var index = paragraphs.FindIndex(p => string.Concat(p.Descendants(WordXml.Word + "t").Select(t => t.Value))
            .TrimStart().StartsWith(StarterDocument.TagSheetMarker, StringComparison.OrdinalIgnoreCase));
        return index < 0 ? null : new Diagnostic(DiagnosticCodes.TagSheetLeftIn,
            "The tag sheet from the starter document is still in this document. Its tags would be filled in and sent to every recipient.",
            $"Delete the page that starts \"{StarterDocument.TagSheetMarker}\", and everything after it, then save and upload the document again.",
            new DiagnosticLocation("document", index + 1, StarterDocument.TagSheetMarker));
    }

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
