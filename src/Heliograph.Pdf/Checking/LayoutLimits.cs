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
    private const string Route = "Heliograph's own PDF converter, which this site uses,";
    private const string Instead = "or ask whoever runs this site to make this letter's PDFs with LibreOffice until it is supported.";

    private static readonly XNamespace A = "http://schemas.openxmlformats.org/drawingml/2006/main";
    private static readonly XNamespace V = "urn:schemas-microsoft-com:vml";
    private static readonly XNamespace Mc = "http://schemas.openxmlformats.org/markup-compatibility/2006";

    private static readonly Limit[] Limits =
    [
        new(DiagnosticCodes.PdfCannotDrawPicture, Pictures, _ => $"This document has a picture. {Route} does not draw pictures yet, so its PDFs would be missing it.",
            $"Take the picture out, {Instead}"),
        new(DiagnosticCodes.PdfCannotDrawShape, Shapes, what => $"This document has {what}. {Route} does not draw text boxes, shapes or charts yet, so its PDFs would be missing it.",
            $"Put the text in an ordinary paragraph or table instead, {Instead}"),
        new(DiagnosticCodes.PdfCannotLayOutNotes, Notes, what => $"This document has {what}. {Route} does not lay out footnotes or endnotes yet.",
            $"Put the note's text in the body of the letter, {Instead}"),
        new(DiagnosticCodes.PdfCannotLayOutSections, Sections, _ => $"This document has more than one section. {Route} lays out one section only, so pages after a section break would take the first section's page and margins.",
            $"Remove the section break, keeping one page size and set of margins for the whole letter, {Instead}"),
        new(DiagnosticCodes.PdfCannotLayOutColumns, Columns, _ => $"This document sets text in columns. {Route} does not lay out columns yet.",
            $"Set the text in a single column, or use a table, {Instead}"),
        new(DiagnosticCodes.PdfCannotWorkOutField, Fields, what => $"This document has a {what} field. {Route} works out page numbers and page counts only, so this field would print nothing.",
            $"Type the text in, or use a tag from the dictionary for a value, {Instead}"),
        new(DiagnosticCodes.PdfCannotMakeLink, Links, _ => $"This document has a link. {Route} does not make links yet: the text would print, but could not be clicked.",
            $"Write the address out in full as plain text, {Instead}"),
        new(DiagnosticCodes.PdfCannotDrawFormatting, Formatting, what => $"This document uses {what}. {Route} does not draw that yet, so it would print as plain text.",
            $"Take that formatting off, {Instead}"),
        new(DiagnosticCodes.PdfCannotLayOutDirection, Direction, what => $"This document has {what}. {Route} lays out text left to right only.",
            $"Set the text left to right, {Instead}"),
        new(DiagnosticCodes.PdfCannotLayOutTable, Tables, what => $"This document has a table with {what}. {Route} does not lay those out yet.",
            $"Give each cell its own row, with no table inside another, {Instead}"),
        new(DiagnosticCodes.PdfCannotAlignTab, Tabs, what => $"This document has a tab stop {what}. {Route} lines tabs up on the left only, so text after it would not line up.",
            $"Use a left tab stop, or a table with the column aligned as you need, {Instead}"),
        new(DiagnosticCodes.PdfFontDrawnInLookAlike, Fonts, what => $"This document uses the font {what}, which {Route} does not have. It is drawn in Liberation Sans, so lines may break in different places from your word processor.",
            "Use Arial, Times New Roman or Courier New, which are drawn in look-alikes with the same widths.", Warning: true),
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

        return diagnostics;
    }

    private static IEnumerable<(XElement, string)> Pictures(XElement root) =>
        root.Descendants().Where(e => (e.Name == A + "graphicData" && ((string?)e.Attribute("uri") ?? "").EndsWith("/picture", StringComparison.Ordinal))
            || e.Name == V + "imagedata").Select(e => (e, "a picture"));

    private static IEnumerable<(XElement, string)> Shapes(XElement root) =>
        root.Descendants().Where(e => (e.Name == A + "graphicData" && !((string?)e.Attribute("uri") ?? "").EndsWith("/picture", StringComparison.Ordinal))
            || e.Name == Mc + "AlternateContent" || (e.Name.Namespace == V && e.Name.LocalName is "shape" or "rect" or "roundrect" or "oval" or "line" && !e.Descendants(V + "imagedata").Any()))
            .Select(e => (e, "a text box, shape or chart"));

    private static IEnumerable<(XElement, string)> Notes(XElement root) =>
        root.Descendants().Where(e => e.Name == W + "footnoteReference" || e.Name == W + "endnoteReference")
            .Select(e => (e, e.Name.LocalName == "footnoteReference" ? "a footnote" : "an endnote"));

    private static IEnumerable<(XElement, string)> Sections(XElement root) =>
        root.Descendants(W + "sectPr").Where(s => s.Parent?.Name == W + "pPr").Select(s => (s, "a section break"));

    private static IEnumerable<(XElement, string)> Columns(XElement root) =>
        root.Descendants(W + "cols").Where(c => (int?)c.Attribute(W + "num") > 1).Select(c => (c, "columns"));

    private static IEnumerable<(XElement, string)> Fields(XElement root) =>
        root.Descendants().Where(e => e.Name == W + "instrText" || e.Name == W + "fldSimple")
            .Select(e => (e, Name: (e.Name == W + "fldSimple" ? (string?)e.Attribute(W + "instr") ?? "" : e.Value).Trim().Split(' ', 2)[0].ToUpperInvariant()))
            .Where(f => f.Name.Length > 0 && f.Name is not ("PAGE" or "NUMPAGES"));

    private static IEnumerable<(XElement, string)> Links(XElement root) =>
        root.Descendants(W + "hyperlink").Select(e => (e, "a link"));

    private static IEnumerable<(XElement, string)> Direction(XElement root) =>
        root.Descendants().Where(e => (e.Name == W + "bidi" && On(e)) || (e.Name == W + "rtl" && On(e))
            || (e.Name == W + "textDirection" && Val(e) is { } d && d is not ("lrTb" or "tb")))
            .Select(e => (e, e.Name.LocalName == "textDirection" ? "text that runs up or down the page" : "right-to-left text"));

    private static IEnumerable<(XElement, string)> Tables(XElement root) =>
        root.Descendants().Where(e => e.Name == W + "vMerge" || (e.Name == W + "tbl" && e.Ancestors(W + "tbl").Any()))
            .Select(e => (e, e.Name.LocalName == "vMerge" ? "cells merged down a column" : "another table inside it"));

    private static IEnumerable<(XElement, string)> Tabs(XElement root) =>
        root.Descendants(W + "tab").Where(t => t.Parent?.Name == W + "tabs")
            .Select(t => (t, Val(t) switch
            {
                "right" or "end" => "aligned right",
                "center" => "centred",
                "decimal" => "aligned on the decimal point",
                "bar" => "drawn as a bar",
                _ => (string?)t.Attribute(W + "leader") is { } leader && leader != "none" ? "with a leader of dots or a line" : "",
            }))
            .Where(t => t.Item2.Length > 0);

    private static IEnumerable<(XElement, string)> Formatting(XElement root) =>
        root.Descendants().Select(e => (e, What: FormattingName(e))).Where(f => f.What is not null)!;

    private static string? FormattingName(XElement e) => e.Name.LocalName switch
    {
        "color" when e.Parent?.Name == W + "rPr" && Val(e) is { } c && c is not ("auto" or "000000") => "coloured text",
        "highlight" when Val(e) is not ("none" or null) => "highlighted text",
        "shd" when (string?)e.Attribute(W + "fill") is { } fill && fill is not ("auto" or "FFFFFF" or "ffffff") => "shading",
        "vertAlign" when Val(e) is "superscript" or "subscript" => "superscript or subscript",
        "strike" or "dstrike" when On(e) => "strikethrough",
        "caps" or "smallCaps" when On(e) => "capitals set as formatting",
        "spacing" when e.Parent?.Name == W + "rPr" && Val(e) is { } s && s != "0" => "spaced-out characters",
        "pBdr" => "a border round a paragraph",
        _ => null,
    };

    private static IEnumerable<(XElement, string)> Fonts(XElement root) =>
        root.Descendants(W + "rFonts")
            .Select(f => (f, Font: (string?)f.Attribute(W + "ascii") ?? (f.Attribute(W + "asciiTheme") is not null ? "set by the document's theme" : null)))
            .Where(f => f.Font is not null && !FontLibrary.HasLookAlike(f.Font))!;

    // Different headers on even pages only apply when the document turns them on.
    private static Diagnostic? EvenPages(DocumentToCheck document)
    {
        var on = document.Settings?.Root?.Element(W + "evenAndOddHeaders") is { } setting && On(setting);
        return !on ? null : new Diagnostic(DiagnosticCodes.PdfCannotLayOutEvenPages,
            $"This document has different headers and footers on even pages. {Route} uses the same header and footer on every page after the first.",
            $"Turn off different odd and even pages in the header and footer settings, {Instead}");
    }
}
