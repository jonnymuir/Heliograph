namespace Heliograph.Pdf.Fonts;

/// <summary>
/// The fonts a host gives the converter, each file read once: by its family and whether it is bold or italic, as the font
/// itself says. A style the host gives no face for is drawn in the family's regular face.
/// </summary>
internal sealed class HostFonts
{
    private readonly Dictionary<(string Family, bool Bold, bool Italic), TrueTypeFont> _faces = [];

    public HostFonts(IEnumerable<byte[]> files)
    {
        foreach (var file in files)
        {
            var face = Read(file);
            _faces[(face.Family.ToUpperInvariant(), face.Bold, face.Italic)] = face;
        }
    }

    public bool Has(string family) => _faces.Keys.Any(k => k.Family.Equals(family, StringComparison.OrdinalIgnoreCase));

    public TrueTypeFont? Face(string family, bool bold, bool italic)
    {
        var key = family.ToUpperInvariant();
        return _faces.GetValueOrDefault((key, bold, italic)) ?? _faces.GetValueOrDefault((key, false, false));
    }

    // A font file the converter can draw and embed: TrueType outlines, a family name, and a licence that allows embedding.
    private static TrueTypeFont Read(byte[] file)
    {
        TrueTypeFont face;
        try
        {
            face = new TrueTypeFont(file);
        }
        catch (Exception e) when (e is InvalidDataException or ArgumentException or IndexOutOfRangeException or KeyNotFoundException)
        {
            throw new ArgumentException("A font given to the converter is not a font file it can read.", nameof(file), e);
        }

        if (!face.Data.Has("glyf") || face.Family.Length == 0)
        {
            throw new ArgumentException($"The font {face.PostScriptName} given to the converter has no TrueType outlines or no family name; give it a .ttf file.", nameof(file));
        }

        return face.Embeddable
            ? face
            : throw new ArgumentException($"The font {face.PostScriptName} given to the converter does not allow embedding, and a PDF must embed its fonts.", nameof(file));
    }
}
