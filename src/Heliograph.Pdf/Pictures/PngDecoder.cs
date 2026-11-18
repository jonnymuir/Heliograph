using System.Buffers.Binary;
using System.IO.Compression;

namespace Heliograph.Pdf.Pictures;

/// <summary>
/// Decodes a PNG to 8-bit samples a PDF can hold: every bit depth and colour type, interlaced or not, with transparency
/// (an alpha channel, or a palette or colour marked transparent) split out as its own channel. Throws
/// <see cref="InvalidDataException"/> for a file it cannot read; the compile-time check has already refused those.
/// </summary>
internal static class PngDecoder
{
    private static readonly byte[] Signature = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

    // Adam7: each pass's first column and row, and its steps across and down.
    private static readonly (int X, int Y, int Dx, int Dy)[] Passes = [(0, 0, 8, 8), (4, 0, 8, 8), (0, 4, 4, 8), (2, 0, 4, 4), (0, 2, 2, 4), (1, 0, 2, 2), (0, 1, 1, 2)];

    public static bool IsPng(ReadOnlySpan<byte> bytes) => bytes.StartsWith(Signature);

    /// <summary>The width and height in pixels, read from the header alone.</summary>
    public static (int Width, int Height) Size(byte[] png)
    {
        var header = Chunks(png).FirstOrDefault(c => c.Type == "IHDR").Data;
        return header is { Length: >= 13 }
            ? (BinaryPrimitives.ReadInt32BigEndian(header), BinaryPrimitives.ReadInt32BigEndian(header.AsSpan(4)))
            : throw new InvalidDataException("The picture is not a PNG file Heliograph can read.");
    }

    public static Raster Decode(byte[] png)
    {
        var info = PngInfo.Read(Chunks(png));
        var data = Inflate(info);
        var raster = new RasterBuilder(info);
        var offset = 0;
        foreach (var (x0, y0, dx, dy) in info.Interlaced ? Passes : [(0, 0, 1, 1)])
        {
            var (columns, rows) = ((info.Width - x0 + dx - 1) / dx, (info.Height - y0 + dy - 1) / dy);
            if (columns == 0 || rows == 0)
            {
                continue;
            }

            var stride = ((columns * info.BitsPerPixel) + 7) / 8;
            var previous = new byte[stride];
            for (var row = 0; row < rows; row++)
            {
                var line = Unfilter(data, offset, stride, previous, Math.Max(1, info.BitsPerPixel / 8));
                offset += stride + 1;
                for (var column = 0; column < columns; column++)
                {
                    raster.Set(x0 + (column * dx), y0 + (row * dy), line, column);
                }

                previous = line;
            }
        }

        return raster.Build();
    }

    private static IEnumerable<(string Type, byte[] Data)> Chunks(byte[] png)
    {
        if (!IsPng(png))
        {
            throw new InvalidDataException("The picture is not a PNG file.");
        }

        for (var at = Signature.Length; at + 8 <= png.Length;)
        {
            var length = BinaryPrimitives.ReadInt32BigEndian(png.AsSpan(at));
            if (length < 0 || at + 12L + length > png.Length)
            {
                throw new InvalidDataException("The PNG file is cut short.");
            }

            var type = System.Text.Encoding.ASCII.GetString(png, at + 4, 4);
            yield return (type, png[(at + 8)..(at + 8 + length)]);
            if (type == "IEND")
            {
                yield break;
            }

            at += 12 + length;
        }
    }

    // The image data, unpacked no further than its expected size, so a small file cannot unpack without limit.
    private static byte[] Inflate(PngInfo info)
    {
        var expected = info.Interlaced
            ? Passes.Sum(p => RawSize(info, (info.Width - p.X + p.Dx - 1) / p.Dx, (info.Height - p.Y + p.Dy - 1) / p.Dy))
            : RawSize(info, info.Width, info.Height);
        using var z = new ZLibStream(new MemoryStream(info.Compressed), CompressionMode.Decompress);
        var data = new byte[expected];
        var read = 0;
        for (int n; read < expected && (n = z.Read(data, read, (int)expected - read)) > 0;)
        {
            read += n;
        }

        return read == expected ? data : throw new InvalidDataException("The PNG file's image data is cut short.");
    }

    private static long RawSize(PngInfo info, int columns, int rows) =>
        columns == 0 || rows == 0 ? 0 : (long)rows * ((((long)columns * info.BitsPerPixel) + 7) / 8 + 1);

    // One scanline with its filter undone: none, sub, up, average or Paeth.
    private static byte[] Unfilter(byte[] data, int offset, int stride, byte[] previous, int bpp)
    {
        var filter = data[offset];
        var line = data.AsSpan(offset + 1, stride).ToArray();
        for (var i = 0; i < stride; i++)
        {
            int a = i >= bpp ? line[i - bpp] : 0, b = previous[i], c = i >= bpp ? previous[i - bpp] : 0;
            line[i] += filter switch
            {
                1 => (byte)a,
                2 => (byte)b,
                3 => (byte)((a + b) / 2),
                4 => (byte)Paeth(a, b, c),
                0 => 0,
                _ => throw new InvalidDataException("The PNG file uses an unknown filter."),
            };
        }

        return line;
    }

    private static int Paeth(int a, int b, int c)
    {
        var p = a + b - c;
        var (pa, pb, pc) = (Math.Abs(p - a), Math.Abs(p - b), Math.Abs(p - c));
        return pa <= pb && pa <= pc ? a : pb <= pc ? b : c;
    }
}
