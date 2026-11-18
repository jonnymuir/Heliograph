using System.Xml.Linq;

namespace Heliograph.Documents;

/// <summary>
/// Pictures without alternative text. A PDF route that tags pictures can only tell a screen reader what one shows if the
/// author has said, so a picture with no description, not marked as decorative, is a warning in every part it is in.
/// </summary>
internal static class PictureDescriptions
{
    private static readonly XNamespace Wp = "http://schemas.openxmlformats.org/drawingml/2006/wordprocessingDrawing";
    private static readonly XNamespace Decorative = "http://schemas.microsoft.com/office/drawing/2017/decorative";

    public static IEnumerable<Diagnostic> Missing(DocxPackage package, IReadOnlyList<(string Name, string Label)> parts)
    {
        foreach (var (name, label) in parts)
        {
            var xml = package.Xml(name)!;
            var undescribed = xml.Descendants(Wp + "docPr").FirstOrDefault(Undescribed);
            if (undescribed is not null)
            {
                yield return new Diagnostic(DiagnosticCodes.PictureHasNoDescription,
                    "This picture has no alternative text, so a screen reader will skip it in an accessible PDF.",
                    "Describe the picture: right-click it, then Edit Alt Text in Word, or Properties > Options > Alternative (Text only) "
                    + "in LibreOffice. If it is only decoration, mark it as decorative in Word.",
                    new DocumentPart(label, xml).Locate(undescribed), DiagnosticSeverity.Warning);
            }
        }
    }

    /// <summary>A picture with no description that is not marked as decorative.</summary>
    internal static bool Undescribed(XElement docPr) =>
        string.IsNullOrWhiteSpace((string?)docPr.Attribute("descr"))
        && !docPr.Descendants(Decorative + "decorative").Any(d => (string?)d.Attribute("val") is "1" or "true");
}
