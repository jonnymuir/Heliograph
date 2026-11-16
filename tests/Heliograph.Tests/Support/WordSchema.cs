using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Validation;

namespace Heliograph.Tests.Support;

/// <summary>
/// Checks a .docx against the format's own schema with Microsoft's Open XML validator. It knows rules the engine
/// was not written with in mind, which is the point: it is an independent check on what a render produces.
/// </summary>
internal static class WordSchema
{
    /// <summary>Every way the file breaks the schema, each as one line of text that does not depend on where it is.</summary>
    public static IReadOnlyList<string> Problems(byte[] docx)
    {
        using var stream = new MemoryStream(docx);
        using var document = WordprocessingDocument.Open(stream, isEditable: false);
        return [.. new OpenXmlValidator().Validate(document)
            .Select(error => $"{error.Part?.Uri}: <{error.Node?.LocalName}> {error.Id}: {error.Description}")
            .Distinct()
            .Order(StringComparer.Ordinal)];
    }
}
