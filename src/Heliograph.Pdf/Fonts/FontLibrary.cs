using System.Collections.Concurrent;
using Heliograph.Pdf.Reading;

namespace Heliograph.Pdf.Fonts;

/// <summary>
/// The fonts a letter is drawn in. A font named in the document is drawn in a built-in look-alike with the same
/// widths: Arial and Helvetica in Liberation Sans, Times New Roman in Liberation Serif, Courier New in Liberation Mono,
/// Calibri in Carlito and Cambria in Caladea, the same files LibreOffice uses for them. Any other font is drawn in
/// Liberation Sans for now. Fonts are read once and shared.
/// </summary>
internal static class FontLibrary
{
    private static readonly ConcurrentDictionary<string, TrueTypeFont> Loaded = new(StringComparer.Ordinal);

    // The host's own fonts for the conversion or check under way, drawn in before any look-alike.
    private static readonly AsyncLocal<HostFonts?> Host = new();

    /// <summary>Uses a host's own fonts until the returned scope ends.</summary>
    public static IDisposable Use(HostFonts? fonts)
    {
        var before = Host.Value;
        Host.Value = fonts;
        return new Scope(() => Host.Value = before);
    }

    // Each font a document may name, in capitals, and the built-in family that draws it. Symbol is only ever a bullet's
    // font; the bullet is drawn in the text's font.
    private static readonly Dictionary<string, string> LookAlikes = new(StringComparer.Ordinal)
    {
        ["ARIAL"] = "LiberationSans", ["HELVETICA"] = "LiberationSans", ["LIBERATION SANS"] = "LiberationSans", ["ARIMO"] = "LiberationSans",
        ["TIMES NEW ROMAN"] = "LiberationSerif", ["TIMES"] = "LiberationSerif", ["LIBERATION SERIF"] = "LiberationSerif", ["TINOS"] = "LiberationSerif",
        ["COURIER NEW"] = "LiberationMono", ["COURIER"] = "LiberationMono", ["LIBERATION MONO"] = "LiberationMono", ["COUSINE"] = "LiberationMono",
        ["CALIBRI"] = "Carlito", ["CARLITO"] = "Carlito",
        ["CAMBRIA"] = "Caladea", ["CALADEA"] = "Caladea",
        ["SYMBOL"] = "LiberationSans",
    };

    public static TrueTypeFont Face(RunStyle style)
    {
        if (Host.Value?.Face(style.Font, style.Bold, style.Italic) is { } own)
        {
            return own;
        }

        var name = $"{Family(style.Font)}-{(style.Bold, style.Italic) switch
        {
            (true, true) => "BoldItalic",
            (true, false) => "Bold",
            (false, true) => "Italic",
            _ => "Regular",
        }}";
        return Loaded.GetOrAdd(name, Load);
    }

    /// <summary>
    /// How far a style reaches above and below the baseline, and the gap under a line, as LibreOffice measures a font:
    /// its ascent from its line table (hhea), its descent as Windows measures it (OS/2), and the rest of the line table's
    /// height as the gap. Text in a line of exact or at-least height stands on that descent, without the gap. For
    /// Liberation and Caladea the two descents agree; Carlito, like Calibri, has a deeper Windows descent and a smaller gap.
    /// </summary>
    public static (double Ascent, double Descent, double Gap) Metrics(RunStyle style)
    {
        // Superscript and subscript reach exactly as far as the same text at full size (TextLook), so the size is the run's.
        var face = Face(style);
        var scale = style.Size / face.UnitsPerEm;
        var gap = Math.Max(0, face.LineGap - face.Descender - face.WinDescent);
        return (face.Ascender * scale, face.WinDescent * scale, gap * scale);
    }

    // Where a character the run's font lacks is looked for next, in this order.
    private static readonly string[] Fallbacks = ["Liberation Sans", "Liberation Serif", "Liberation Mono", "Carlito", "Caladea"];

    /// <summary>
    /// The style that draws a character: the run's own when its font has it, else the same style in the first built-in font
    /// that does; null when none does.
    /// </summary>
    public static RunStyle? Covering(RunStyle style, int character) =>
        Face(style).Has(character) ? style : Fallbacks.Select(font => style with { Font = font }).FirstOrDefault(s => Face(s).Has(character));

    /// <summary>Whether a font named in a document has a built-in look-alike, rather than falling back to Liberation Sans.</summary>
    public static bool HasLookAlike(string font) => font.Length == 0 || Known(font) is not null || Host.Value?.Has(font) == true;

    private static string Family(string font) => Known(font) ?? "LiberationSans";

    private static string? Known(string font) => LookAlikes.GetValueOrDefault(font.ToUpperInvariant());

    private static TrueTypeFont Load(string name)
    {
        using var stream = typeof(FontLibrary).Assembly.GetManifestResourceStream($"Heliograph.Pdf.Fonts.{name}.ttf")
            ?? throw new InvalidOperationException($"The built-in font {name} is missing from the package.");
        using var bytes = new MemoryStream();
        stream.CopyTo(bytes);
        return new TrueTypeFont(bytes.ToArray());
    }

    private sealed class Scope(Action end) : IDisposable
    {
        public void Dispose() => end();
    }
}
