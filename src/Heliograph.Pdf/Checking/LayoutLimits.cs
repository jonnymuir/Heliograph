using System.Xml.Linq;
using Heliograph.Documents;
using Heliograph.Pdf.Fonts;
using static Heliograph.Pdf.Reading.WordXml;

namespace Heliograph.Pdf.Checking;

/// <summary>
/// What Heliograph's own PDF converter cannot lay out yet, found when a template is compiled, so no PDF is ever quietly
/// missing something. Each is reported once in each part, at the first place it occurs. The list shrinks as the
/// converter grows (docs/pdf-engine.md).
/// </summary>
internal static class LayoutLimits
{
    internal const string Route = "Heliograph's own PDF converter, which this site uses,";
    internal const string Instead = "or ask whoever runs this site to make this letter's PDFs with LibreOffice until it is supported.";

    private static readonly XNamespace A = "http://schemas.openxmlformats.org/drawingml/2006/main";
    private static readonly XNamespace V = "urn:schemas-microsoft-com:vml";
    private static readonly XNamespace Wp = "http://schemas.openxmlformats.org/drawingml/2006/wordprocessingDrawing";
    private static readonly XNamespace Mc = "http://schemas.openxmlformats.org/markup-compatibility/2006";

    private static readonly Limit[] Limits = [.. ThingsOnThePage(), .. ThingsInTheText()];

    // Objects and page structure.
    private static Limit[] ThingsOnThePage() =>
    [
        new(DiagnosticCodes.PdfCannotDrawPicture, Pictures, what => $"This document has {what}. {Route} draws pictures in line with the text, or floating in front of or behind it, only, so its PDFs would be missing it.",
            $"Set the picture in line with text, or in front of or behind it (Wrap Text in Word), {Instead}"),
        new(DiagnosticCodes.PdfCannotDrawShape, Shapes, what => $"This document has {what}. {Route} does not draw text boxes, shapes or charts yet, so its PDFs would be missing it.",
            $"Put the text in an ordinary paragraph or table instead, {Instead}"),
        new(DiagnosticCodes.PdfCannotLayOutNotes, NoteLimits.Found, what => $"This document has {what}. {Route} lays out footnotes in the body of the letter, numbered 1, 2, 3 at the foot of the page, only.",
            $"Put the note's text in the body of the letter, or make it an ordinary footnote there, {Instead}"),
        new(DiagnosticCodes.PdfCannotLayOutSections, SectionLimits.Found, what => $"This document has {what}. {Route} does not lay that out yet.",
            $"Use a section break that starts a new page, with the page settings Word gives a new document otherwise, {Instead}"),
        new(DiagnosticCodes.PdfCannotLayOutColumns, Columns, _ => $"This document sets text in columns. {Route} does not lay out columns yet.",
            $"Set the text in a single column, or use a table, {Instead}"),
        new(DiagnosticCodes.PdfCannotWorkOutField, Fields, what => $"This document has a {what} field. {Route} works out page numbers and page counts only, so this field would print nothing.",
            $"Type the text in, or use a tag from the dictionary for a value, {Instead}"),
    ];

    // How text is set.
    private static Limit[] ThingsInTheText() =>
    [
        new(DiagnosticCodes.PdfCannotDrawFormatting, FormattingLimits.Found, what => $"This document uses {what}. {Route} does not draw that yet, so it would print as plain text.",
            $"Take that formatting off, {Instead}"),
        new(DiagnosticCodes.PdfCannotLayOutDirection, Direction, what => $"This document has {what}. {Route} lays out text left to right only.",
            $"Set the text left to right, {Instead}"),
        new(DiagnosticCodes.PdfCannotLayOutTable, Tables, what => $"This document has a table with {what}. {Route} does not lay those out yet.",
            $"Give each cell its own row, with no table inside another, {Instead}"),
        new(DiagnosticCodes.PdfCannotAlignTab, Tabs, what => $"This document has a tab stop {what}. {Route} lines tabs up on the left only, so text after it would not line up.",
            $"Use a left tab stop, or a table with the column aligned as you need, {Instead}"),
    ];

    public static IReadOnlyList<Diagnostic> Check(DocumentToCheck document)
    {
        var diagnostics = new List<Diagnostic>();
        foreach (var part in document.Parts)
        {
            foreach (var limit in Limits)
            {
                if (limit.Found(part.Xml.Root!).FirstOrDefault() is ({ } place, var what))
                {
                    diagnostics.Add(new Diagnostic(limit.Code, limit.Message(what), limit.Fix, part.Locate(place),
                        limit.Warning ? DiagnosticSeverity.Warning : DiagnosticSeverity.Error));
                }
            }
        }

        if (EvenPages(document) is { } even)
        {
            diagnostics.Add(even);
        }

        // LibreOffice turns automatic hyphenation on in the settings of every document it saves, and off again for every
        // paragraph in the defaults; only the two together hyphenate.
        var suppressed = On(document.Styles?.Root?.Element(W + "docDefaults")?.Element(W + "pPrDefault")?.Element(W + "pPr")?.Element(W + "suppressAutoHyphens"));
        if (On(document.Settings?.Root?.Element(W + "autoHyphenation")) && !suppressed)
        {
            diagnostics.Add(new Diagnostic(DiagnosticCodes.PdfCannotDrawFormatting,
                $"This document hyphenates words automatically. {Route} does not hyphenate yet, so lines would break in different places.",
                $"Turn off automatic hyphenation (Layout > Hyphenation in Word), {Instead}"));
        }

        diagnostics.AddRange(NoteLimits.Check(document));
        diagnostics.AddRange(ListLimits.Check(document));
        diagnostics.AddRange(PictureLimits.Check(document));
        diagnostics.AddRange(LinkLimits.Check(document));
        diagnostics.AddRange(TableLimits.Check(document));
        diagnostics.AddRange(GlyphLimits.Check(document));
        diagnostics.AddRange(FontLimits.Check(document));

        return diagnostics;
    }

    // A picture in line with the text is drawn (PictureLimits checks its file); one that floats, or an old-style VML
    // picture, is not yet.
    // A picture in line with the text, or floating with no text wrapped round it outside a table, is drawn (PictureLimits
    // checks its file); one with text wrapped round it, one floating in a table, or an old-style VML picture is not yet.
    private static IEnumerable<(XElement, string)> Pictures(XElement root)
    {
        var floating = root.Descendants(Wp + "anchor").Where(a => a.Descendants(A + "graphicData").Any(g => ((string?)g.Attribute("uri") ?? "").EndsWith("/picture", StringComparison.Ordinal)))
            .Select(a => (a, What: a.Element(Wp + "wrapNone") is null ? "a picture with text wrapped round it" : a.Ancestors(W + "tbl").Any() ? "a picture that floats in a table" : null))
            .Where(f => f.What is not null);
        return floating.Concat(root.Descendants(V + "imagedata").Select(e => (e, (string?)"an old-style picture")))!;
    }

    private static IEnumerable<(XElement, string)> Shapes(XElement root)
    {
        return root.Descendants().Where(e => (e.Name == A + "graphicData" && !((string?)e.Attribute("uri") ?? "").EndsWith("/picture", StringComparison.Ordinal))
            || e.Name == Mc + "AlternateContent" || (e.Name.Namespace == V && e.Name.LocalName is "shape" or "rect" or "roundrect" or "oval" or "line" && !e.Descendants(V + "imagedata").Any()))
            .Select(e => (e, "a text box, shape or chart"));
    }

    private static IEnumerable<(XElement, string)> Columns(XElement root)
    {
        return root.Descendants(W + "cols").Where(c => (int?)c.Attribute(W + "num") > 1).Select(c => (c, "columns"));
    }

    private static IEnumerable<(XElement, string)> Fields(XElement root)
    {
        return root.Descendants().Where(e => e.Name == W + "instrText" || e.Name == W + "fldSimple")
            .Select(e => (e, Name: (e.Name == W + "fldSimple" ? (string?)e.Attribute(W + "instr") ?? "" : e.Value).Trim().Split(' ', 2)[0].ToUpperInvariant()))
            .Where(f => f.Name.Length > 0 && f.Name is not ("PAGE" or "NUMPAGES"));
    }

    private static IEnumerable<(XElement, string)> Direction(XElement root)
    {
        return root.Descendants().Where(e => (e.Name == W + "bidi" && On(e)) || (e.Name == W + "rtl" && On(e))
            || (e.Name == W + "textDirection" && Val(e) is { } d && d is not ("lrTb" or "tb")))
            .Select(e => (e, e.Name.LocalName == "textDirection" ? "text that runs up or down the page" : "right-to-left text"));
    }

    private static IEnumerable<(XElement, string)> Tables(XElement root)
    {
        return root.Descendants().Where(e => e.Name == W + "tbl" && e.Ancestors(W + "tbl").Any()).Select(e => (e, "another table inside it"));
    }

    private static IEnumerable<(XElement, string)> Tabs(XElement root)
    {
        var aligned = root.Descendants(W + "tab").Where(t => t.Parent?.Name == W + "tabs")
            .Select(t => (t, Val(t) == "bar" ? "drawn as a bar" : ""));

        // A tab in a centred or right-aligned paragraph follows rules of its own in Word, not laid out yet.
        var inAligned = root.Descendants(W + "tab").Where(t => t.Parent?.Name == W + "r"
            && Val(t.Ancestors(W + "p").FirstOrDefault()?.Element(W + "pPr")?.Element(W + "jc")) is "center" or "right" or "end")
            .Select(t => (t, "in a centred or right-aligned paragraph"));
        return aligned.Concat(inAligned).Where(t => t.Item2.Length > 0);
    }

    // Different headers on even pages only apply when the document turns them on.
    private static Diagnostic? EvenPages(DocumentToCheck document)
    {
        var on = document.Settings?.Root?.Element(W + "evenAndOddHeaders") is { } setting && On(setting);
        return !on ? null : new Diagnostic(DiagnosticCodes.PdfCannotLayOutEvenPages,
            $"This document has different headers and footers on even pages. {Route} uses the same header and footer on every page after the first.",
            $"Turn off different odd and even pages in the header and footer settings, {Instead}");
    }
}
