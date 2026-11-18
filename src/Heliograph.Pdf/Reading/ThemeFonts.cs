using System.Xml.Linq;
using static Heliograph.Pdf.Reading.WordXml;

namespace Heliograph.Pdf.Reading;

/// <summary>
/// The document theme's two fonts: the minor font for body text and the major font for headings. Most Word documents name
/// their fonts only through the theme (Calibri, or Aptos since 2024), so a run's font set as "minor" or "major" is the
/// theme's. A theme font takes precedence over a font named alongside it, as in Word.
/// </summary>
internal sealed record ThemeFonts(string? Minor, string? Major)
{
    private static readonly XNamespace A = "http://schemas.openxmlformats.org/drawingml/2006/main";

    public static readonly ThemeFonts None = new(null, null);

    public static ThemeFonts Read(XDocument? theme)
    {
        var scheme = theme?.Descendants(A + "fontScheme").FirstOrDefault();
        return new ThemeFonts(
            (string?)scheme?.Element(A + "minorFont")?.Element(A + "latin")?.Attribute("typeface"),
            (string?)scheme?.Element(A + "majorFont")?.Element(A + "latin")?.Attribute("typeface"));
    }

    /// <summary>The font a run's fonts element names: its theme font when it has one, else its own; null for neither.</summary>
    public string? Of(XElement rFonts)
    {
        var theme = (string?)rFonts.Attribute(W + "asciiTheme");
        return theme switch
        {
            null => (string?)rFonts.Attribute(W + "ascii"),
            _ when theme.StartsWith("major", StringComparison.Ordinal) => Major,
            _ => Minor,
        };
    }

    /// <summary>Writes each theme font in a part as the font it stands for, so the rest of the reader sees a name.</summary>
    public XDocument? Resolve(XDocument? part)
    {
        foreach (var rFonts in part?.Descendants(W + "rFonts").Where(f => f.Attribute(W + "asciiTheme") is not null) ?? [])
        {
            if (Of(rFonts) is { } font)
            {
                rFonts.SetAttributeValue(W + "ascii", font);
            }
        }

        return part;
    }
}
