using System.Buffers.Binary;
using System.Text;

namespace Heliograph.Pdf.Fonts;

/// <summary>The bytes of a TrueType font and its table directory, read big-endian as the format is written.</summary>
internal sealed class FontData
{
    private readonly Dictionary<string, (int Offset, int Length)> _tables = [];

    public FontData(byte[] bytes)
    {
        Bytes = bytes;
        var count = U16(4);
        for (var i = 0; i < count; i++)
        {
            var at = 12 + (i * 16);
            _tables[Encoding.ASCII.GetString(bytes, at, 4)] = ((int)U32(at + 8), (int)U32(at + 12));
        }
    }

    public byte[] Bytes { get; }

    public bool Has(string tag) => _tables.ContainsKey(tag);

    public int Offset(string tag) => _tables.TryGetValue(tag, out var t) ? t.Offset : throw new InvalidDataException($"The font has no {tag} table.");

    public byte[] Table(string tag) => Bytes.AsSpan(_tables[tag].Offset, _tables[tag].Length).ToArray();

    public ushort U16(int at) => BinaryPrimitives.ReadUInt16BigEndian(Bytes.AsSpan(at));

    public short S16(int at) => BinaryPrimitives.ReadInt16BigEndian(Bytes.AsSpan(at));

    public uint U32(int at) => BinaryPrimitives.ReadUInt32BigEndian(Bytes.AsSpan(at));
}
