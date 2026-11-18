using System.Xml.Linq;
using static Heliograph.Pdf.Reading.WordXml;

namespace Heliograph.Pdf.Reading;

/// <summary>What one element of a run adds: text, a tab, a line break, a picture or a footnote's mark.</summary>
internal static class RunContent
{
    /// <summary>The inline an element of a run stands for; null for anything else, and for hidden text, which a letter sends still hidden.</summary>
    public static Inline? Inline(XElement c, RunStyle style, PartLinks links) => c.Name.Namespace != W || style.Hidden ? null : c.Name.LocalName switch
    {
        "footnoteReference" => new NoteInline(links.Notes.Refer((string?)c.Attribute(W + "id") ?? ""), (string?)c.Attribute(W + "id") ?? "", style),
        "footnoteRef" => new TextInline(links.Notes.Mark(), style),
        "t" => new TextInline(style.Caps ? c.Value.ToUpperInvariant() : c.Value, style),
        "tab" => new TabInline(style),
        "br" or "cr" => new BreakInline(style),
        "drawing" => PictureReader.Read(c, links.Files, style),
        _ => null,
    };
}
