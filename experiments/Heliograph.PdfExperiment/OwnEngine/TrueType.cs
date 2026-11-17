using System.Buffers.Binary;
using System.Text;

namespace Heliograph.PdfExperiment.OwnEngine;

/// <summary>
/// A TrueType font, read for what layout and embedding need: glyph for each character, advance widths, vertical
/// metrics, and a subset that keeps only the glyphs a document used. Glyph ids are kept as they are (unused glyphs are
/// emptied), so the PDF can use the identity mapping from character id to glyph id.
/// </summary>
public sealed class TrueType
{
    private readonly byte[] _data;
    private readonly Dictionary<string, (int Offset, int Length)> _tables = [];
    private readonly Dictionary<int, ushort> _cmap = [];
    private readonly ushort[] _advances;

    public TrueType(byte[] data)
    {
        _data = data;
        var count = U16(4);
        for (var i = 0; i < count; i++)
        {
            var at = 12 + i * 16;
            _tables[Encoding.ASCII.GetString(data, at, 4)] = ((int)U32(at + 8), (int)U32(at + 12));
        }

        var head = _tables["head"].Offset;
        UnitsPerEm = U16(head + 18);
        BBox = [S16(head + 36), S16(head + 38), S16(head + 40), S16(head + 42)];
        LongLoca = S16(head + 50) == 1;
        var hhea = _tables["hhea"].Offset;
        var metrics = U16(hhea + 34);
        HheaAscender = S16(hhea + 4);
        HheaDescender = S16(hhea + 6);
        HheaLineGap = S16(hhea + 8);
        NumGlyphs = U16(_tables["maxp"].Offset + 4);
        _advances = new ushort[NumGlyphs];
        var hmtx = _tables["hmtx"].Offset;
        for (var g = 0; g < NumGlyphs; g++) _advances[g] = U16(hmtx + Math.Min(g, metrics - 1) * 4);
        var os2 = _tables["OS/2"].Offset;
        WinAscent = U16(os2 + 74);
        WinDescent = U16(os2 + 76);
        TypoAscender = S16(os2 + 68);
        TypoDescender = S16(os2 + 70);
        CapHeight = U16(os2) >= 2 ? S16(os2 + 88) : TypoAscender;
        Weight = U16(os2 + 4);
        var post = _tables["post"].Offset;
        ItalicAngle = (int)BinaryPrimitives.ReadInt32BigEndian(data.AsSpan(post + 4)) / 65536.0;
        PostScriptName = Name(6) ?? "Font";
        ReadCmap();
    }

    public int UnitsPerEm { get; }
    public int[] BBox { get; }
    public int NumGlyphs { get; }
    public int HheaAscender { get; }
    public int HheaDescender { get; }
    public int HheaLineGap { get; }
    public int WinAscent { get; }
    public int WinDescent { get; }
    public int TypoAscender { get; }
    public int TypoDescender { get; }
    public int CapHeight { get; }
    public int Weight { get; }
    public double ItalicAngle { get; }
    public string PostScriptName { get; }
    private bool LongLoca { get; }

    public ushort Glyph(int codePoint) => _cmap.TryGetValue(codePoint, out var g) ? g : (ushort)0;

    /// <summary>The advance of a glyph in thousandths of the font size, as PDF widths are given.</summary>
    public double Advance(ushort glyph) => _advances[glyph] * 1000.0 / UnitsPerEm;

    public double Width(string text, double size)
    {
        double units = 0;
        foreach (var rune in text.EnumerateRunes()) units += _advances[Glyph(rune.Value)];
        return units * size / UnitsPerEm;
    }

    /// <summary>The distance from one line to the next at this size, as Word lays out single spacing.</summary>
    public double LineHeight(double size) => (HheaAscender - HheaDescender + HheaLineGap) * size / UnitsPerEm;

    public double Ascent(double size) => HheaAscender * size / UnitsPerEm;

    /// <summary>A copy of the font in which every glyph not listed is empty. Glyph ids do not change.</summary>
    public byte[] Subset(IEnumerable<ushort> used)
    {
        var keep = new HashSet<ushort>(used) { 0 };
        var glyf = _tables["glyf"].Offset;
        foreach (var g in keep.ToList()) AddComponents(g, keep, glyf);

        var newGlyf = new MemoryStream();
        var offsets = new int[NumGlyphs + 1];
        for (var g = 0; g < NumGlyphs; g++)
        {
            offsets[g] = (int)newGlyf.Length;
            var (start, end) = GlyphRange(g);
            if (keep.Contains((ushort)g) && end > start)
            {
                newGlyf.Write(_data, glyf + start, end - start);
                while (newGlyf.Length % 4 != 0) newGlyf.WriteByte(0);
            }
        }

        offsets[NumGlyphs] = (int)newGlyf.Length;
        var loca = new byte[(NumGlyphs + 1) * 4];
        for (var g = 0; g <= NumGlyphs; g++) BinaryPrimitives.WriteUInt32BigEndian(loca.AsSpan(g * 4), (uint)offsets[g]);

        var head = Table("head").ToArray();
        BinaryPrimitives.WriteInt16BigEndian(head.AsSpan(50), 1); // long loca
        BinaryPrimitives.WriteUInt32BigEndian(head.AsSpan(8), 0);

        var tables = new SortedDictionary<string, byte[]>(StringComparer.Ordinal)
        {
            ["head"] = head, ["hhea"] = Table("hhea").ToArray(), ["maxp"] = Table("maxp").ToArray(), ["hmtx"] = Table("hmtx").ToArray(),
            ["loca"] = loca, ["glyf"] = newGlyf.ToArray(), ["cmap"] = Table("cmap").ToArray(), ["post"] = Table("post").ToArray(),
            ["OS/2"] = Table("OS/2").ToArray(), ["name"] = Table("name").ToArray(),
        };
        foreach (var hinting in new[] { "cvt ", "fpgm", "prep" })
            if (_tables.ContainsKey(hinting)) tables[hinting] = Table(hinting).ToArray();

        return Assemble(tables);
    }

    private static byte[] Assemble(SortedDictionary<string, byte[]> tables)
    {
        var count = tables.Count;
        var search = 1;
        var selector = 0;
        while (search * 2 <= count) { search *= 2; selector++; }
        var output = new MemoryStream();
        var header = new byte[12 + count * 16];
        BinaryPrimitives.WriteUInt32BigEndian(header, 0x00010000);
        BinaryPrimitives.WriteUInt16BigEndian(header.AsSpan(4), (ushort)count);
        BinaryPrimitives.WriteUInt16BigEndian(header.AsSpan(6), (ushort)(search * 16));
        BinaryPrimitives.WriteUInt16BigEndian(header.AsSpan(8), (ushort)selector);
        BinaryPrimitives.WriteUInt16BigEndian(header.AsSpan(10), (ushort)(count * 16 - search * 16));
        output.Write(header);
        var i = 0;
        var headAt = 0;
        foreach (var (tag, bytes) in tables)
        {
            var at = 12 + i++ * 16;
            Encoding.ASCII.GetBytes(tag).CopyTo(header, at);
            BinaryPrimitives.WriteUInt32BigEndian(header.AsSpan(at + 4), Checksum(bytes));
            BinaryPrimitives.WriteUInt32BigEndian(header.AsSpan(at + 8), (uint)output.Length);
            BinaryPrimitives.WriteUInt32BigEndian(header.AsSpan(at + 12), (uint)bytes.Length);
            if (tag == "head") headAt = (int)output.Length;
            output.Write(bytes);
            while (output.Length % 4 != 0) output.WriteByte(0);
        }

        var font = output.ToArray();
        header.CopyTo(font, 0);
        BinaryPrimitives.WriteUInt32BigEndian(font.AsSpan(headAt + 8), 0xB1B0AFBA - Checksum(font));
        return font;
    }

    private static uint Checksum(byte[] bytes)
    {
        uint sum = 0;
        for (var i = 0; i < bytes.Length; i += 4)
        {
            uint word = 0;
            for (var j = 0; j < 4; j++) word = (word << 8) | (i + j < bytes.Length ? bytes[i + j] : 0u);
            sum += word;
        }

        return sum;
    }

    private void AddComponents(ushort glyph, HashSet<ushort> keep, int glyf)
    {
        var (start, end) = GlyphRange(glyph);
        if (end <= start || S16(glyf + start) >= 0) return;
        var at = glyf + start + 10;
        while (true)
        {
            var flags = U16(at);
            var component = U16(at + 2);
            if (keep.Add(component)) AddComponents(component, keep, glyf);
            at += 4 + ((flags & 1) != 0 ? 4 : 2);
            if ((flags & 8) != 0) at += 2;
            else if ((flags & 0x40) != 0) at += 4;
            else if ((flags & 0x80) != 0) at += 8;
            if ((flags & 0x20) == 0) break;
        }
    }

    private (int Start, int End) GlyphRange(int g)
    {
        var loca = _tables["loca"].Offset;
        return LongLoca ? ((int)U32(loca + g * 4), (int)U32(loca + g * 4 + 4)) : (U16(loca + g * 2) * 2, U16(loca + g * 2 + 2) * 2);
    }

    private void ReadCmap()
    {
        var cmap = _tables["cmap"].Offset;
        int? best = null;
        for (var i = 0; i < U16(cmap + 2); i++)
        {
            var at = cmap + 4 + i * 8;
            var (platform, encoding) = (U16(at), U16(at + 2));
            var offset = cmap + (int)U32(at + 4);
            var format = U16(offset);
            if (platform == 3 && encoding == 10 && format == 12) { best = offset; break; }
            if (platform == 3 && encoding == 1 && format == 4) best = offset;
        }

        if (best is not { } table) return;
        if (U16(table) == 12)
        {
            var groups = (int)U32(table + 12);
            for (var i = 0; i < groups; i++)
            {
                var at = table + 16 + i * 12;
                var (start, end, glyph) = (U32(at), U32(at + 4), U32(at + 8));
                for (var c = start; c <= end && c < 0x30000; c++) _cmap[(int)c] = (ushort)(glyph + c - start);
            }

            return;
        }

        var segments = U16(table + 6) / 2;
        var ends = table + 14;
        var starts = ends + segments * 2 + 2;
        var deltas = starts + segments * 2;
        var ranges = deltas + segments * 2;
        for (var s = 0; s < segments; s++)
        {
            int end = U16(ends + s * 2), start = U16(starts + s * 2);
            var delta = S16(deltas + s * 2);
            int range = U16(ranges + s * 2);
            for (var c = start; c <= end && c != 0xFFFF; c++)
            {
                int glyph;
                if (range == 0) glyph = (c + delta) & 0xFFFF;
                else
                {
                    var at = ranges + s * 2 + range + (c - start) * 2;
                    glyph = U16(at);
                    if (glyph != 0) glyph = (glyph + delta) & 0xFFFF;
                }

                if (glyph != 0) _cmap[c] = (ushort)glyph;
            }
        }
    }

    private string? Name(int id)
    {
        var name = _tables["name"].Offset;
        var count = U16(name + 2);
        var strings = name + U16(name + 4);
        for (var i = 0; i < count; i++)
        {
            var at = name + 6 + i * 12;
            if (U16(at + 6) != id) continue;
            var (platform, length, offset) = (U16(at), U16(at + 8), U16(at + 10));
            return platform == 3 ? Encoding.BigEndianUnicode.GetString(_data, strings + offset, length) : Encoding.ASCII.GetString(_data, strings + offset, length);
        }

        return null;
    }

    private ReadOnlySpan<byte> Table(string tag) => _data.AsSpan(_tables[tag].Offset, _tables[tag].Length);
    private ushort U16(int at) => BinaryPrimitives.ReadUInt16BigEndian(_data.AsSpan(at));
    private short S16(int at) => BinaryPrimitives.ReadInt16BigEndian(_data.AsSpan(at));
    private uint U32(int at) => BinaryPrimitives.ReadUInt32BigEndian(_data.AsSpan(at));
}
