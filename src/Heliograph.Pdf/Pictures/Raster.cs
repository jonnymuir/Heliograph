using System.Buffers.Binary;

namespace Heliograph.Pdf.Pictures;

/// <summary>A decoded picture: 8-bit grey or RGB samples, row by row, and an alpha channel when any pixel is see-through.</summary>
internal sealed record Raster(int Width, int Height, int Channels, byte[] Color, byte[]? Alpha);

/// <summary>What a PNG's header, palette and transparency chunks say, and its image data joined up.</summary>
internal sealed record PngInfo(int Width, int Height, int BitDepth, int ColorType, bool Interlaced, byte[] Palette, byte[] Transparency, byte[] Compressed)
{
    /// <summary>The most pixels a picture may have: about 6,000 by 4,000. A larger one is refused at compile.</summary>
    public const long MaxPixels = 25_000_000;

    public int SamplesPerPixel => ColorType switch { 2 => 3, 4 => 2, 6 => 4, _ => 1 };

    public int BitsPerPixel => SamplesPerPixel * BitDepth;

    public static PngInfo Read(IEnumerable<(string Type, byte[] Data)> chunks)
    {
        byte[]? header = null, palette = null, transparency = null;
        using var compressed = new MemoryStream();
        foreach (var (type, data) in chunks)
        {
            switch (type)
            {
                case "IHDR": header = data; break;
                case "PLTE": palette = data; break;
                case "tRNS": transparency = data; break;
                case "IDAT": compressed.Write(data); break;
            }
        }

        if (header is not { Length: >= 13 })
        {
            throw new InvalidDataException("The PNG file has no header.");
        }

        var info = new PngInfo(BinaryPrimitives.ReadInt32BigEndian(header), BinaryPrimitives.ReadInt32BigEndian(header.AsSpan(4)), header[8], header[9],
            header[12] == 1, palette ?? [], transparency ?? [], compressed.ToArray());
        return info.Valid() ? info : throw new InvalidDataException("The PNG file is not one Heliograph can read.");
    }

    private bool Valid()
    {
        var depths = ColorType switch
        {
            0 => new[] { 1, 2, 4, 8, 16 },
            3 => [1, 2, 4, 8],
            2 or 4 or 6 => [8, 16],
            _ => [],
        };
        return Width > 0 && Height > 0 && (long)Width * Height <= MaxPixels && depths.Contains(BitDepth) && (ColorType != 3 || Palette.Length >= 3);
    }
}

/// <summary>Fills a <see cref="Raster"/> pixel by pixel from unfiltered PNG scanlines.</summary>
internal sealed class RasterBuilder(PngInfo info)
{
    private readonly int _channels = info.ColorType is 0 or 4 ? 1 : 3;
    private readonly byte[] _color = new byte[(long)info.Width * info.Height * (info.ColorType is 0 or 4 ? 1 : 3)];
    private readonly byte[] _alpha = new byte[(long)info.Width * info.Height];
    private bool _seeThrough;

    public void Set(int x, int y, byte[] line, int column)
    {
        var at = (y * info.Width) + x;
        Span<int> raw = stackalloc int[4];
        for (var s = 0; s < info.SamplesPerPixel; s++)
        {
            raw[s] = Sample(line, (column * info.SamplesPerPixel) + s);
        }

        var alpha = info.ColorType switch
        {
            3 => Indexed(raw[0], at),
            4 => Grey(at, raw[0], Scale(raw[1])),
            6 => Rgb(at, raw, Scale(raw[3])),
            2 => Rgb(at, raw, Keyed(raw[..3]) ? (byte)0 : (byte)255),
            _ => Grey(at, raw[0], Keyed(raw[..1]) ? (byte)0 : (byte)255),
        };
        _alpha[at] = alpha;
        _seeThrough |= alpha != 255;
    }

    public Raster Build() => new(info.Width, info.Height, _channels, _color, _seeThrough ? _alpha : null);

    private byte Grey(int at, int sample, byte alpha)
    {
        _color[at] = Scale(sample);
        return alpha;
    }

    private byte Rgb(int at, ReadOnlySpan<int> raw, byte alpha)
    {
        for (var c = 0; c < 3; c++)
        {
            _color[(at * 3) + c] = Scale(raw[c]);
        }

        return alpha;
    }

    // The n-th sample of a scanline, at the file's bit depth.
    private int Sample(byte[] line, int n) => info.BitDepth switch
    {
        16 => (line[n * 2] << 8) | line[(n * 2) + 1],
        8 => line[n],
        _ => (line[n * info.BitDepth / 8] >> (8 - info.BitDepth - (n * info.BitDepth % 8))) & ((1 << info.BitDepth) - 1),
    };

    private byte Scale(int sample) => info.BitDepth switch
    {
        16 => (byte)(sample >> 8),
        8 => (byte)sample,
        _ => (byte)(sample * 255 / ((1 << info.BitDepth) - 1)),
    };

    private byte Indexed(int index, int at)
    {
        var entry = Math.Min(index, (info.Palette.Length / 3) - 1) * 3;
        for (var c = 0; c < 3; c++)
        {
            _color[(at * 3) + c] = info.Palette[entry + c];
        }

        return index < info.Transparency.Length ? info.Transparency[index] : (byte)255;
    }

    // A grey or RGB PNG may name one colour as see-through.
    private bool Keyed(ReadOnlySpan<int> raw)
    {
        if (info.Transparency.Length < raw.Length * 2)
        {
            return false;
        }

        for (var i = 0; i < raw.Length; i++)
        {
            if (BinaryPrimitives.ReadUInt16BigEndian(info.Transparency.AsSpan(i * 2)) != raw[i])
            {
                return false;
            }
        }

        return true;
    }
}
