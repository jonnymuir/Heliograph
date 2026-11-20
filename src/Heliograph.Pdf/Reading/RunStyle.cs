namespace Heliograph.Pdf.Reading;

/// <summary>
/// How a run is set. Colour and Background are RGB hex ("C00000"), null for automatic and none; Strike is 0, 1 or 2
/// lines; VertAlign is "baseline", "superscript" or "subscript"; CharacterSpacing is points added after each character.
/// </summary>
internal sealed record RunStyle(bool Bold, bool Italic, bool Underline, double Size, string Font)
{
    public string? Color { get; init; }

    public string? Background { get; init; }

    public int Strike { get; init; }

    public string VertAlign { get; init; } = "baseline";

    public bool Caps { get; init; }

    /// <summary>How an underlined run is underlined: "single", "words" (not under the spaces), "double" or "thick".</summary>
    public string UnderlineStyle { get; init; } = "single";

    /// <summary>The underline's own colour, as RGB hex, or null for the text's.</summary>
    public string? UnderlineColor { get; init; }

    /// <summary>Small capitals: lowercase letters drawn as capitals at a smaller size.</summary>
    public bool SmallCaps { get; init; }

    public double CharacterSpacing { get; init; }

    public bool Hidden { get; init; }

    /// <summary>The address the run links to, when it is in a link.</summary>
    public string? Link { get; init; }
}
