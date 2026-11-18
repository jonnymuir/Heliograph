using System.Xml.Linq;
using Heliograph.Documents;
using Heliograph.Pdf.Pictures;

namespace Heliograph.Pdf.Checking;

/// <summary>
/// Pictures in line with the text whose file the converter cannot draw: not a PNG or JPEG, damaged, or too large to
/// decode safely. A PNG is decoded in full here, so nothing about a picture can fail when a letter is made. Reported
/// once in each part, at the first such picture.
/// </summary>
internal static class PictureLimits
{
    private static readonly XNamespace Wp = "http://schemas.openxmlformats.org/drawingml/2006/wordprocessingDrawing";
    private static readonly XNamespace A = "http://schemas.openxmlformats.org/drawingml/2006/main";
    private static readonly XNamespace R = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";

    public static IEnumerable<Diagnostic> Check(DocumentToCheck document)
    {
        foreach (var part in document.Parts)
        {
            // A picture with no file in the document, such as one linked to a file elsewhere, is a missing file.
            var found = part.Xml.Descendants().Where(e => e.Name == Wp + "inline" || e.Name == Wp + "anchor")
                .Where(inline => inline.Descendants(A + "graphicData").Any(g => ((string?)g.Attribute("uri") ?? "").EndsWith("/picture", StringComparison.Ordinal)))
                .Select(inline => (Inline: inline, Embed: (string?)inline.Descendants(A + "blip").FirstOrDefault()?.Attribute(R + "embed")))
                .Select(p => (p.Inline, Problem: PictureFile.Problem(p.Embed is null ? null : part.Linked(p.Embed))))
                .FirstOrDefault(p => p.Problem is not null);
            if (found.Problem is { } what)
            {
                yield return new Diagnostic(DiagnosticCodes.PdfCannotDrawPicture,
                    $"This document has {what}. {LayoutLimits.Route} draws PNG and JPEG pictures only.",
                    $"Save the picture as a PNG or JPEG and insert it again, {LayoutLimits.Instead}", part.Locate(found.Inline));
            }
        }
    }
}
