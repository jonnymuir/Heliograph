using System.Security.Cryptography;
using System.Text;
using Heliograph.Pdf.Fonts;
using static Heliograph.Pdf.Writing.PdfObjects;

namespace Heliograph.Pdf.Writing;

/// <summary>
/// The fonts a PDF uses: one resource per face, each embedded as a subset of the glyphs drawn, with widths and a map
/// back to Unicode so text can be copied, searched and read aloud.
/// </summary>
internal sealed class FontEmbedder(PdfObjects objects)
{
    private readonly Dictionary<TrueTypeFont, (string Name, int Id, Dictionary<ushort, string> Glyphs)> _faces = [];

    /// <summary>The resource name for a face, and the glyph ids of a text as hex, noting which glyphs are used.</summary>
    public (string Name, int Id, string Hex) Use(TrueTypeFont face, string text)
    {
        if (!_faces.TryGetValue(face, out var entry))
        {
            _faces[face] = entry = (Invariant($"F{_faces.Count + 1}"), objects.Reserve(), []);
        }

        var hex = new StringBuilder();
        foreach (var rune in text.EnumerateRunes())
        {
            var glyph = face.Glyph(rune.Value);
            entry.Glyphs.TryAdd(glyph, rune.ToString());
            hex.Append(glyph.ToString("X4", System.Globalization.CultureInfo.InvariantCulture));
        }

        return (entry.Name, entry.Id, hex.ToString());
    }

    public void WriteAll()
    {
        foreach (var (face, (_, id, glyphs)) in _faces)
        {
            Write(face, id, glyphs);
        }
    }

    private void Write(TrueTypeFont face, int id, Dictionary<ushort, string> glyphs)
    {
        var subset = FontSubsetter.Subset(face, glyphs.Keys);
        // A subset's name starts with six capital letters, here taken from its content so it never changes for the same text.
        var tag = new string([.. SHA256.HashData(subset).Take(6).Select(b => (char)('A' + (b % 26)))]);
        var baseFont = $"{tag}+{face.PostScriptName}";
        var file = objects.Stream(subset, Invariant($" /Length1 {subset.Length}"));
        var descriptor = objects.Reserve();
        objects.Set(descriptor, Descriptor(face, baseFont, file));
        var widths = string.Join(" ", glyphs.Keys.Order().Select(g => Invariant($"{g} [{N(face.Advance(g))}]")));
        var cid = objects.Reserve();
        objects.Set(cid, Invariant($"<</Type /Font /Subtype /CIDFontType2 /BaseFont /{baseFont} /CIDSystemInfo <</Registry (Adobe) /Ordering (Identity) /Supplement 0>> ") +
            Invariant($"/FontDescriptor {descriptor} 0 R /W [{widths}] /DW 1000 /CIDToGIDMap /Identity>>"));
        var toUnicode = objects.Stream(Encoding.ASCII.GetBytes(UnicodeMap(glyphs)));
        objects.Set(id, Invariant($"<</Type /Font /Subtype /Type0 /BaseFont /{baseFont} /Encoding /Identity-H /DescendantFonts [{cid} 0 R] /ToUnicode {toUnicode} 0 R>>"));
    }

    private static string Descriptor(TrueTypeFont face, string baseFont, int file)
    {
        double S(int units) => units * 1000.0 / face.UnitsPerEm;
        var flags = 32 | (face.ItalicAngle != 0 ? 64 : 0);
        var box = string.Join(" ", face.BBox.Select(b => N(S(b))));
        return Invariant($"<</Type /FontDescriptor /FontName /{baseFont} /Flags {flags} /FontBBox [{box}] /ItalicAngle {N(face.ItalicAngle)} ") +
            Invariant($"/Ascent {N(S(face.Ascender))} /Descent {N(S(face.Descender))} /CapHeight {N(S(face.CapHeight))} /StemV {(face.Weight >= 600 ? 120 : 80)} /FontFile2 {file} 0 R>>");
    }

    private static string UnicodeMap(Dictionary<ushort, string> glyphs)
    {
        var cmap = new StringBuilder("/CIDInit /ProcSet findresource begin 12 dict begin begincmap ")
            .Append("/CIDSystemInfo <</Registry (Adobe) /Ordering (UCS) /Supplement 0>> def /CMapName /Adobe-Identity-UCS def /CMapType 2 def ")
            .Append("1 begincodespacerange <0000> <FFFF> endcodespacerange\n");
        foreach (var chunk in glyphs.OrderBy(g => g.Key).Chunk(100))
        {
            cmap.Append(Invariant($"{chunk.Length} beginbfchar\n"));
            foreach (var (glyph, text) in chunk)
            {
                cmap.Append(Invariant($"<{glyph:X4}> <{Convert.ToHexString(Encoding.BigEndianUnicode.GetBytes(text))}>\n"));
            }

            cmap.Append("endbfchar\n");
        }

        return cmap.Append("endcmap CMapName currentdict /CMap defineresource pop end end").ToString();
    }
}
