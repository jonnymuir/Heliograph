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

    private static string Family(string font) => font.ToUpperInvariant() switch
    {
        "TIMES NEW ROMAN" or "TIMES" or "LIBERATION SERIF" or "TINOS" => "LiberationSerif",
        "COURIER NEW" or "COURIER" or "LIBERATION MONO" or "COUSINE" => "LiberationMono",
        _ => "LiberationSans",
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
