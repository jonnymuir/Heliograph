using System.Collections.Concurrent;
using Heliograph.Pdf.Reading;

namespace Heliograph.Pdf.Fonts;

/// <summary>
/// The fonts a letter is drawn in. A font named in the document is drawn in a built-in look-alike with the same
/// widths: Arial and Helvetica in Liberation Sans, Times New Roman in Liberation Serif, Courier New in Liberation Mono.
/// Any other font is drawn in Liberation Sans for now. Fonts are read once and shared.
/// </summary>
internal static class FontLibrary
{
    private static readonly ConcurrentDictionary<string, TrueTypeFont> Loaded = new(StringComparer.Ordinal);

    public static TrueTypeFont Face(RunStyle style)
    {
        var name = $"{Family(style.Font)}-{(style.Bold, style.Italic) switch
        {
            (true, true) => "BoldItalic",
            (true, false) => "Bold",
            (false, true) => "Italic",
            _ => "Regular",
        }}";
        return Loaded.GetOrAdd(name, Load);
    }

    /// <summary>Whether a font named in a document has a built-in look-alike, rather than falling back to Liberation Sans.</summary>
    public static bool HasLookAlike(string font) => font.Length == 0 || Known(font) is not null;

    private static string Family(string font) => Known(font) ?? "LiberationSans";

    private static string? Known(string font) => font.ToUpperInvariant() switch
    {
        "ARIAL" or "HELVETICA" or "LIBERATION SANS" or "ARIMO" => "LiberationSans",
        "TIMES NEW ROMAN" or "TIMES" or "LIBERATION SERIF" or "TINOS" => "LiberationSerif",
        "COURIER NEW" or "COURIER" or "LIBERATION MONO" or "COUSINE" => "LiberationMono",
        // Symbol is only ever a bullet's font; the bullet is drawn in the text's font.
        "SYMBOL" => "LiberationSans",
        _ => null,
    };

    private static TrueTypeFont Load(string name)
    {
        using var stream = typeof(FontLibrary).Assembly.GetManifestResourceStream($"Heliograph.Pdf.Fonts.{name}.ttf")
            ?? throw new InvalidOperationException($"The built-in font {name} is missing from the package.");
        using var bytes = new MemoryStream();
        stream.CopyTo(bytes);
        return new TrueTypeFont(bytes.ToArray());
    }
}
