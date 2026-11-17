namespace Heliograph.Pdf.Fonts;

/// <summary>A font's map from Unicode character to glyph, read from its Windows cmap subtable (format 12, else 4).</summary>
internal static class CharacterMap
{
    public static Dictionary<int, ushort> Read(FontData font)
    {
        var map = new Dictionary<int, ushort>();
        var cmap = font.Offset("cmap");
        int? best = null;
        for (var i = 0; i < font.U16(cmap + 2); i++)
        {
            var at = cmap + 4 + (i * 8);
            var offset = cmap + (int)font.U32(at + 4);
            var (platform, encoding, format) = (font.U16(at), font.U16(at + 2), font.U16(offset));
            if (platform == 3 && encoding == 10 && format == 12)
            {
                best = offset;
                break;
            }

            if (platform == 3 && encoding == 1 && format == 4)
            {
                best = offset;
            }
        }

        if (best is { } table)
        {
            if (font.U16(table) == 12)
            {
                Format12(font, table, map);
            }
            else
            {
                Format4(font, table, map);
            }
        }

        return map;
    }

    private static void Format12(FontData font, int table, Dictionary<int, ushort> map)
    {
        var groups = (int)font.U32(table + 12);
        for (var i = 0; i < groups; i++)
        {
            var at = table + 16 + (i * 12);
            var (start, end, glyph) = (font.U32(at), font.U32(at + 4), font.U32(at + 8));
            for (var c = start; c <= end && c <= 0x10FFFF; c++)
            {
                map[(int)c] = (ushort)(glyph + c - start);
            }
        }
    }

    private static void Format4(FontData font, int table, Dictionary<int, ushort> map)
    {
        var segments = font.U16(table + 6) / 2;
        var ends = table + 14;
        var starts = ends + (segments * 2) + 2;
        var deltas = starts + (segments * 2);
        var ranges = deltas + (segments * 2);
        for (var s = 0; s < segments; s++)
        {
            int end = font.U16(ends + (s * 2)), start = font.U16(starts + (s * 2)), range = font.U16(ranges + (s * 2));
            var delta = font.S16(deltas + (s * 2));
            for (var c = start; c <= end && c != 0xFFFF; c++)
            {
                var glyph = range == 0 ? (c + delta) & 0xFFFF : font.U16(ranges + (s * 2) + range + ((c - start) * 2));
                if (range != 0 && glyph != 0)
                {
                    glyph = (glyph + delta) & 0xFFFF;
                }

                if (glyph != 0)
                {
                    map[c] = (ushort)glyph;
                }
            }
        }
    }
}
