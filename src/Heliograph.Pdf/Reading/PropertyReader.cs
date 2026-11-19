using System.Xml.Linq;
using static Heliograph.Pdf.Reading.WordXml;

namespace Heliograph.Pdf.Reading;

/// <summary>
/// Resolves run and paragraph properties: each set in the list overrides the ones before it, from the document
/// defaults through the style chain to what is written on the paragraph or run itself.
/// </summary>
internal static class PropertyReader
{
    public static RunStyle Run(IEnumerable<XElement?> layers)
    {
        var style = new RunStyle(false, false, false, 10, "Liberation Sans");
        foreach (var x in layers.OfType<XElement>())
        {
            style = Apply(style, x);
        }

        return style;
    }

    private static RunStyle Apply(RunStyle style, XElement x)
    {
        if (x.Element(W + "b") is { } b)
        {
            style = style with { Bold = On(b) };
        }

        if (x.Element(W + "i") is { } i)
        {
            style = style with { Italic = On(i) };
        }

        if (x.Element(W + "u") is { } u)
        {
            var colour = (string?)u.Attribute(W + "color");
            style = style with
            {
                Underline = Val(u) is not ("none" or null),
                UnderlineStyle = Val(u) is "words" or "double" or "thick" ? Val(u)! : "single",
                UnderlineColor = colour is { Length: 6 } && colour != "auto" ? colour.ToUpperInvariant() : null,
            };
        }

        if (Val(x.Element(W + "sz")) is { } sz)
        {
            style = style with { Size = Number(sz) / 2 };
        }

        if ((string?)x.Element(W + "rFonts")?.Attribute(W + "ascii") is { } font)
        {
            style = style with { Font = font };
        }

        return RunFormatting.Apply(style, x);
    }

    public static ParagraphStyle Paragraph(IReadOnlyList<XElement> layers)
    {
        int? outline = null;
        var align = "left";
        foreach (var x in layers)
        {
            if (Val(x.Element(W + "outlineLvl")) is { } o)
            {
                outline = (int)Number(o);
            }

            if (Val(x.Element(W + "jc")) is { } jc)
            {
                align = jc switch { "start" => "left", "end" => "right", _ => jc };
            }
        }

        return new ParagraphStyle(outline, SpacingOf(layers), IndentOf(layers), align, FlowOf(layers), TabStopReader.Read(layers));
    }

    private static Spacing SpacingOf(IReadOnlyList<XElement> layers)
    {
        var spacing = new Spacing(0, 0, 240, "auto");
        foreach (var s in layers.Select(x => x.Element(W + "spacing")).OfType<XElement>())
        {
            spacing = new Spacing(
                Twips(s, "before") ?? spacing.Before,
                Twips(s, "after") ?? spacing.After,
                (string?)s.Attribute(W + "line") is { } line ? Number(line) : spacing.Line,
                (string?)s.Attribute(W + "lineRule") ?? spacing.LineRule);
        }

        return spacing;
    }

    private static Indent IndentOf(IReadOnlyList<XElement> layers)
    {
        var indent = new Indent(0, 0, 0);
        foreach (var ind in layers.Select(x => x.Element(W + "ind")).OfType<XElement>())
        {
            var first = Twips(ind, "firstLine") ?? (Twips(ind, "hanging") is { } hanging ? -hanging : indent.FirstLine);
            indent = new Indent(Twips(ind, "left") ?? Twips(ind, "start") ?? indent.Left, Twips(ind, "right") ?? Twips(ind, "end") ?? indent.Right, first);
        }

        return indent;
    }

    private static Flow FlowOf(IReadOnlyList<XElement> layers)
    {
        var flow = new Flow(false, false, true, false);
        foreach (var x in layers)
        {
            flow = new Flow(
                x.Element(W + "keepNext") is { } kn ? On(kn) : flow.KeepNext,
                x.Element(W + "keepLines") is { } kl ? On(kl) : flow.KeepLines,
                x.Element(W + "widowControl") is { } wc ? On(wc) : flow.WidowControl,
                x.Element(W + "pageBreakBefore") is { } pb ? On(pb) : flow.PageBreakBefore);
        }

        return flow;
    }
}
