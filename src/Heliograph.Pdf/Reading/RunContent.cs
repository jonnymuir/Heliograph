using System.Xml.Linq;
using static Heliograph.Pdf.Reading.WordXml;

namespace Heliograph.Pdf.Reading;

/// <summary>What one element of a run adds: text, a tab, a line break, a picture or a footnote's mark.</summary>
internal static class RunContent
{
    /// <summary>The inline an element of a run stands for; null for anything else, and for hidden text, which a letter sends still hidden.</summary>
    public static Inline? Inline(XElement c, RunStyle style, PartLinks links) => c.Name.Namespace != W || style.Hidden ? null : c.Name.LocalName switch
    {
        "t" => new TextInline(style.Caps ? c.Value.ToUpperInvariant() : c.Value, style),
        "tab" => new TabInline(style),
        "br" or "cr" => Break(c, style),
        "drawing" => PictureReader.Read(c, links.Files, style),
        _ => Note(c, style, links),
    };

    // A line break; or a page break, or a column break, which in a single column is one.
    private static BreakInline Break(XElement c, RunStyle style) =>
        new(style) { Page = c.Name.LocalName == "br" && (string?)c.Attribute(W + "type") is "page" or "column" };

    // A reference to a footnote or an endnote in the text, or the mark that opens the note itself: either way, the note's
    // number, each kind counted on its own.
    private static Inline? Note(XElement c, RunStyle style, PartLinks links)
    {
        var (end, name) = c.Name.LocalName.StartsWith("endnote", StringComparison.Ordinal) ? (true, c.Name.LocalName[7..]) : (false, c.Name.LocalName.Replace("footnote", "", StringComparison.Ordinal));
        var numbers = end ? links.Endnotes : links.Notes;
        if (name == "Ref")
        {
            return new TextInline(numbers.Mark(), style);
        }

        var id = name == "Reference" ? (string?)c.Attribute(W + "id") ?? "" : null;
        return id is null ? null : new NoteInline(numbers.Refer(id), id, style) { End = end };
    }
}
