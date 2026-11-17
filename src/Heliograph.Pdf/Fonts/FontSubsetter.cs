using System.Buffers.Binary;
using System.Text;

namespace Heliograph.Pdf.Fonts;

/// <summary>
/// A copy of a font that keeps only the glyphs a document used, and the glyphs those are built from. Glyph ids do not
/// change (unused glyphs are emptied), so the PDF maps character ids to glyph ids one to one.
/// </summary>
internal static class FontSubsetter
{
    private static readonly string[] Copied = ["hhea", "maxp", "hmtx", "cmap", "post", "OS/2", "name", "cvt ", "fpgm", "prep"];

    public static byte[] Subset(TrueTypeFont font, IEnumerable<ushort> used)
    {
        var data = font.Data;
        var keep = new HashSet<ushort>(used) { 0 };
        var glyf = data.Offset("glyf");
        foreach (var g in keep.ToList())
        {
            AddComponents(font, g, keep, glyf);
        }

        var (newGlyf, loca) = Glyphs(font, keep, glyf);
        var head = data.Table("head");
        BinaryPrimitives.WriteInt16BigEndian(head.AsSpan(50), 1);   // offsets are written long
        BinaryPrimitives.WriteUInt32BigEndian(head.AsSpan(8), 0);    // the whole-font checksum is set last

        var tables = new SortedDictionary<string, byte[]>(StringComparer.Ordinal) { ["head"] = head, ["loca"] = loca, ["glyf"] = newGlyf };
        foreach (var tag in Copied.Where(data.Has))
        {
            tables[tag] = data.Table(tag);
        }

        return Assemble(tables);
    }

    private static (byte[] Glyf, byte[] Loca) Glyphs(TrueTypeFont font, HashSet<ushort> keep, int glyf)
    {
        var output = new MemoryStream();
        var loca = new byte[(font.NumGlyphs + 1) * 4];
        for (var g = 0; g < font.NumGlyphs; g++)
        {
            BinaryPrimitives.WriteUInt32BigEndian(loca.AsSpan(g * 4), (uint)output.Length);
            var (start, end) = Range(font, g);
            if (keep.Contains((ushort)g) && end > start)
            {
                output.Write(font.Data.Bytes, glyf + start, end - start);
                while (output.Length % 4 != 0)
                {
                    output.WriteByte(0);
                }
            }
        }

        BinaryPrimitives.WriteUInt32BigEndian(loca.AsSpan(font.NumGlyphs * 4), (uint)output.Length);
        return (output.ToArray(), loca);
    }

    // A composite glyph (an accented letter, say) is drawn from other glyphs, which must be kept too.
    private static void AddComponents(TrueTypeFont font, ushort glyph, HashSet<ushort> keep, int glyf)
    {
        var data = font.Data;
        var (start, end) = Range(font, glyph);
        if (end <= start || data.S16(glyf + start) >= 0)
        {
            return;
        }

        var at = glyf + start + 10;
        ushort flags;
        do
        {
            flags = data.U16(at);
            var component = data.U16(at + 2);
            if (keep.Add(component))
            {
                AddComponents(font, component, keep, glyf);
            }

            at += 4 + ((flags & 1) != 0 ? 4 : 2) + ((flags & 8) != 0 ? 2 : (flags & 0x40) != 0 ? 4 : (flags & 0x80) != 0 ? 8 : 0);
        }
        while ((flags & 0x20) != 0);
    }

    private static (int Start, int End) Range(TrueTypeFont font, int g)
    {
        var data = font.Data;
        var loca = data.Offset("loca");
        var longOffsets = data.S16(data.Offset("head") + 50) == 1;
        return longOffsets
            ? ((int)data.U32(loca + (g * 4)), (int)data.U32(loca + (g * 4) + 4))
            : (data.U16(loca + (g * 2)) * 2, data.U16(loca + (g * 2) + 2) * 2);
    }

    private static byte[] Assemble(SortedDictionary<string, byte[]> tables)
    {
        var count = tables.Count;
        var power = (int)Math.Pow(2, Math.Floor(Math.Log2(count)));
        var header = new byte[12 + (count * 16)];
        BinaryPrimitives.WriteUInt32BigEndian(header, 0x00010000);
        BinaryPrimitives.WriteUInt16BigEndian(header.AsSpan(4), (ushort)count);
        BinaryPrimitives.WriteUInt16BigEndian(header.AsSpan(6), (ushort)(power * 16));
        BinaryPrimitives.WriteUInt16BigEndian(header.AsSpan(8), (ushort)Math.Log2(power));
        BinaryPrimitives.WriteUInt16BigEndian(header.AsSpan(10), (ushort)((count - power) * 16));
        var output = new MemoryStream();
        output.Write(header);
        var i = 0;
        var headAt = 0;
        foreach (var (tag, bytes) in tables)
        {
            var at = 12 + (i++ * 16);
            Encoding.ASCII.GetBytes(tag).CopyTo(header, at);
            BinaryPrimitives.WriteUInt32BigEndian(header.AsSpan(at + 4), Checksum(bytes));
            BinaryPrimitives.WriteUInt32BigEndian(header.AsSpan(at + 8), (uint)output.Length);
            BinaryPrimitives.WriteUInt32BigEndian(header.AsSpan(at + 12), (uint)bytes.Length);
            headAt = tag == "head" ? (int)output.Length : headAt;
            output.Write(bytes);
            while (output.Length % 4 != 0)
            {
                output.WriteByte(0);
            }
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
            for (var j = 0; j < 4; j++)
            {
                word = (word << 8) | (i + j < bytes.Length ? bytes[i + j] : 0u);
            }

            sum += word;
        }

        return sum;
    }
}
