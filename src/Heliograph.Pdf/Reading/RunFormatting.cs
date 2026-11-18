using System.Globalization;
using System.Xml.Linq;
using static Heliograph.Pdf.Reading.WordXml;

namespace Heliograph.Pdf.Reading;

/// <summary>
/// The run properties beyond font, size, bold, italic and underline: colour, highlight and shading, strikethrough,
/// superscript and subscript, capitals, character spacing and hidden text. Each set overrides the ones before it.
/// </summary>
internal static class RunFormatting
{
    // Word's highlight colours, by name.
    private static readonly Dictionary<string, string> Highlights = new(StringComparer.Ordinal)
    {
        ["yellow"] = "FFFF00", ["green"] = "00FF00", ["cyan"] = "00FFFF", ["magenta"] = "FF00FF", ["blue"] = "0000FF", ["red"] = "FF0000",
        ["darkBlue"] = "000080", ["darkCyan"] = "008080", ["darkGreen"] = "008000", ["darkMagenta"] = "800080", ["darkRed"] = "800000",
        ["darkYellow"] = "808000", ["darkGray"] = "808080", ["lightGray"] = "C0C0C0", ["black"] = "000000", ["white"] = "FFFFFF",
    };

    public static RunStyle Apply(RunStyle style, XElement x)
    {
        style = Colours(style, x);
        if (x.Element(W + "strike") is { } strike)
        {
            style = style with { Strike = On(strike) ? 1 : 0 };
        }

        if (x.Element(W + "dstrike") is { } dstrike && On(dstrike))
        {
            style = style with { Strike = 2 };
        }

        return style with
        {
            VertAlign = Val(x.Element(W + "vertAlign")) ?? style.VertAlign,
            Caps = x.Element(W + "caps") is { } caps ? On(caps) : style.Caps,
            SmallCaps = x.Element(W + "smallCaps") is { } smallCaps ? On(smallCaps) : style.SmallCaps,
            Hidden = x.Element(W + "vanish") is { } vanish ? On(vanish) : style.Hidden,
            CharacterSpacing = Twips(x.Element(W + "spacing"), "val") ?? style.CharacterSpacing,
        };
    }

    private static RunStyle Colours(RunStyle style, XElement x)
    {
        if (Val(x.Element(W + "color")) is { } color)
        {
            style = style with { Color = Hex(color) };
        }

        if (x.Element(W + "shd") is { } shd)
        {
            style = style with { Background = Shading(shd) };
        }

        // A highlight is drawn over any shading.
        if (Val(x.Element(W + "highlight")) is { } highlight)
        {
            style = style with { Background = Highlights.GetValueOrDefault(highlight) ?? (highlight == "none" ? style.Background : null) };
        }

        return style;
    }

    // Clear shading is its fill; solid shading is its pattern colour, black when automatic.
    private static string? Shading(XElement shd) => Val(shd) switch
    {
        "solid" => Hex((string?)shd.Attribute(W + "color") ?? "auto") ?? "000000",
        _ => Hex((string?)shd.Attribute(W + "fill") ?? "auto"),
    };

    private static string? Hex(string value) =>
        value.Length == 6 && int.TryParse(value, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out _) ? value.ToUpperInvariant() : null;
}
