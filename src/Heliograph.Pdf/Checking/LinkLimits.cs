using System.Xml.Linq;
using Heliograph.Documents;
using Heliograph.Pdf.Reading;
using static Heliograph.Pdf.Reading.WordXml;

namespace Heliograph.Pdf.Checking;

/// <summary>
/// Links the converter cannot make: one in a header or footer, one to a place in the document, or one to an address that
/// is not a web page, email or telephone number. Reported once in each part, at the first such link.
/// </summary>
internal static class LinkLimits
{
    public static IEnumerable<Diagnostic> Check(DocumentToCheck document)
    {
        foreach (var part in document.Parts)
        {
            var found = part.Xml.Descendants(W + "hyperlink").Select(link => (Link: link, Problem: Problem(part, link))).FirstOrDefault(l => l.Problem is not null);
            if (found.Problem is { } what)
            {
                yield return new Diagnostic(DiagnosticCodes.PdfCannotMakeLink,
                    $"This document has {what}. {LayoutLimits.Route} makes links in the body to web pages, email addresses and telephone numbers only.",
                    $"Write the address out in full as plain text, {LayoutLimits.Instead}", part.Locate(found.Link));
            }
        }
    }

    private static string? Problem(DocumentPart part, XElement link)
    {
        if (part.Label is not ("document" or "footnotes" or "endnotes"))
        {
            return "a link in a header or footer";
        }

        return (string?)link.Attribute(R + "id") is not { } id
            ? "a link to a place in the document"
            : LinkAddress.Safe(part.External(id)) is null ? "a link to an address that is not a web page, email address or telephone number" : null;
    }
}
