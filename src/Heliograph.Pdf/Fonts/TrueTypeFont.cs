using System.Buffers.Binary;
using System.Text;

namespace Heliograph.Pdf.Fonts;

/// <summary>
/// A TrueType font, read for what layout and embedding need: the glyph for each character, advance widths and vertical
/// metrics. Line height follows the font's horizontal header, ascender less descender plus line gap, as Word and
/// LibreOffice lay out single spacing.
/// </summary>
internal sealed class TrueTypeFont
{
    private readonly Dictionary<int, ushort> _cmap;
    private readonly ushort[] _advances;

    public TrueTypeFont(byte[] bytes)
    {
        Data = new FontData(bytes);
        var head = Data.Offset("head");
        UnitsPerEm = Data.U16(head + 18);
        BBox = [Data.S16(head + 36), Data.S16(head + 38), Data.S16(head + 40), Data.S16(head + 42)];
        var hhea = Data.Offset("hhea");
        (Ascender, Descender, LineGap) = (Data.S16(hhea + 4), Data.S16(hhea + 6), Data.S16(hhea + 8));
        NumGlyphs = Data.U16(Data.Offset("maxp") + 4);
        _advances = Advances(Data.U16(hhea + 34));
        var os2 = Data.Offset("OS/2");
        Weight = Data.U16(os2 + 4);
        CapHeight = Data.U16(os2) >= 2 ? Data.S16(os2 + 88) : Ascender;
        ItalicAngle = BinaryPrimitives.ReadInt32BigEndian(Data.Bytes.AsSpan(Data.Offset("post") + 4)) / 65536.0;
        PostScriptName = Name(6) ?? "Font";
        _cmap = CharacterMap.Read(Data);
    }

    public FontData Data { get; }
    public int UnitsPerEm { get; }
    public int[] BBox { get; }
    public int NumGlyphs { get; }
    public int Ascender { get; }
    public int Descender { get; }
    public int LineGap { get; }
    public int CapHeight { get; }
    public int Weight { get; }
    public double ItalicAngle { get; }
    public string PostScriptName { get; }

    public ushort Glyph(int codePoint) => _cmap.TryGetValue(codePoint, out var g) ? g : (ushort)0;

    public bool Has(int codePoint) => _cmap.ContainsKey(codePoint);

    /// <summary>A glyph's advance in thousandths of the font size, as PDF widths are given.</summary>
    public double Advance(ushort glyph) => _advances[glyph] * 1000.0 / UnitsPerEm;

    public double Width(string text, double size)
    {
        double units = 0;
        foreach (var rune in text.EnumerateRunes())
        {
            units += _advances[Glyph(rune.Value)];
        }

        return units * size / UnitsPerEm;
    }

    public double LineHeight(double size) => (Ascender - Descender + LineGap) * size / UnitsPerEm;

    public double Ascent(double size) => Ascender * size / UnitsPerEm;

    private ushort[] Advances(int metrics)
    {
        var advances = new ushort[NumGlyphs];
        var hmtx = Data.Offset("hmtx");
        for (var g = 0; g < NumGlyphs; g++)
        {
            advances[g] = Data.U16(hmtx + (Math.Min(g, metrics - 1) * 4));
        }

        return advances;
    }

    private string? Name(int id)
    {
        var name = Data.Offset("name");
        var strings = name + Data.U16(name + 4);
        for (var i = 0; i < Data.U16(name + 2); i++)
        {
            var at = name + 6 + (i * 12);
            if (Data.U16(at + 6) == id)
            {
                var (platform, length, offset) = (Data.U16(at), Data.U16(at + 8), Data.U16(at + 10));
                var encoding = platform == 3 ? Encoding.BigEndianUnicode : Encoding.ASCII;
                return encoding.GetString(Data.Bytes, strings + offset, length);
            }
        }

        return null;
    }
}
