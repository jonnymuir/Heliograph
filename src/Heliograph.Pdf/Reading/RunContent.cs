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
        "br" or "cr" => new BreakInline(style),
        "drawing" => PictureReader.Read(c, links.Files, style),
        _ => Note(c, style, links.Notes),
    };

    // A reference to a footnote in the text, or the mark that opens the note itself: either way, the note's number.
    private static Inline? Note(XElement c, RunStyle style, NoteNumbers notes)
    {
        if (c.Name.LocalName == "footnoteRef")
        {
            return new TextInline(notes.Mark(), style);
        }

        var id = c.Name.LocalName == "footnoteReference" ? (string?)c.Attribute(W + "id") ?? "" : null;
        return id is null ? null : new NoteInline(notes.Refer(id), id, style);
    }
}
