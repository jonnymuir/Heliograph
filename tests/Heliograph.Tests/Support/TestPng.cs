using System.Buffers.Binary;
using System.IO.Compression;
using System.Text;

namespace Heliograph.Tests.Support;

/// <summary>
/// Writes small PNG files of every colour type and bit depth, interlaced or not, for testing the converter's decoder. It
/// shares no code with it. Each pixel's samples are given at the file's bit depth.
/// </summary>
internal static class TestPng
{
    private static readonly (int X, int Y, int Dx, int Dy)[] Passes = [(0, 0, 8, 8), (4, 0, 8, 8), (0, 4, 4, 8), (2, 0, 4, 4), (0, 2, 2, 4), (1, 0, 2, 2), (0, 1, 1, 2)];

    /// <summary>A PNG of width by height pixels; pixel(x, y) gives that pixel's samples.</summary>
    public static byte[] Write(int width, int height, int colorType, int bitDepth, Func<int, int, int[]> pixel,
        bool interlaced = false, byte[]? palette = null, byte[]? transparency = null)
    {
        using var raw = new MemoryStream();
        foreach (var (x0, y0, dx, dy) in interlaced ? Passes : [(0, 0, 1, 1)])
        {
            for (var y = y0; y < height; y += dy)
            {
                var samples = new List<int>();
                for (var x = x0; x < width; x += dx)
                {
                    samples.AddRange(pixel(x, y));
                }

                if (samples.Count > 0)
                {
                    raw.WriteByte(0);
                    raw.Write(Pack(samples, bitDepth));
                }
            }
        }

        var header = new byte[13];
        BinaryPrimitives.WriteInt32BigEndian(header, width);
        BinaryPrimitives.WriteInt32BigEndian(header.AsSpan(4), height);
        (header[8], header[9], header[12]) = ((byte)bitDepth, (byte)colorType, (byte)(interlaced ? 1 : 0));

        using var png = new MemoryStream();
        png.Write([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]);
        Chunk(png, "IHDR", header);
        if (palette is not null)
        {
            Chunk(png, "PLTE", palette);
        }

        if (transparency is not null)
        {
            Chunk(png, "tRNS", transparency);
        }

        Chunk(png, "IDAT", Deflate(raw.ToArray()));
        Chunk(png, "IEND", []);
        return png.ToArray();
    }

    private static byte[] Pack(List<int> samples, int bitDepth)
    {
        if (bitDepth == 16)
        {
            return [.. samples.SelectMany(s => new[] { (byte)(s >> 8), (byte)s })];
        }

        var bytes = new byte[((samples.Count * bitDepth) + 7) / 8];
        for (var i = 0; i < samples.Count; i++)
        {
            var bit = i * bitDepth;
            bytes[bit / 8] |= (byte)(samples[i] << (8 - bitDepth - (bit % 8)));
        }

        return bytes;
    }

    private static byte[] Deflate(byte[] data)
    {
        using var output = new MemoryStream();
        using (var z = new ZLibStream(output, CompressionLevel.Optimal, leaveOpen: true))
        {
            z.Write(data);
        }

        return output.ToArray();
    }

    private static void Chunk(Stream png, string type, byte[] data)
    {
        var length = new byte[4];
        BinaryPrimitives.WriteInt32BigEndian(length, data.Length);
        png.Write(length);
        var typed = Encoding.ASCII.GetBytes(type).Concat(data).ToArray();
        png.Write(typed);
        var crc = new byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(crc, Crc(typed));
        png.Write(crc);
    }

    // The CRC-32 a PNG chunk ends with (ISO 3309), so other readers accept the file.
    private static uint Crc(byte[] data)
    {
        var crc = 0xFFFFFFFFu;
        foreach (var b in data)
        {
            crc ^= b;
            for (var k = 0; k < 8; k++)
            {
                crc = (crc & 1) != 0 ? 0xEDB88320u ^ (crc >> 1) : crc >> 1;
            }
        }

        return crc ^ 0xFFFFFFFFu;
    }
}
