using System.Xml.Linq;
using static Heliograph.Pdf.Reading.WordXml;

namespace Heliograph.Pdf.Reading;

/// <summary>What one element of a run adds: text, a tab, a line break or a picture.</summary>
internal static class RunContent
{
    /// <summary>The inline an element of a run stands for; null for anything else.</summary>
    public static Inline? Inline(XElement c, RunStyle style, Func<string, byte[]?> linked) => c.Name.Namespace != W ? null : c.Name.LocalName switch
    {
        "t" => new TextInline(style.Caps ? c.Value.ToUpperInvariant() : c.Value, style),
        "tab" => new TabInline(style),
        "br" or "cr" => new BreakInline(style),
        "drawing" => PictureReader.Read(c, linked, style),
        _ => null,
    };
}
