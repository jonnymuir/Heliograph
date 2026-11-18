using System.Buffers.Binary;

namespace Heliograph.Pdf.Pictures;

/// <summary>
/// Reads what a PDF needs to hold a JPEG as it is: its size, its number of colour components, and whether its CMYK is
/// stored inverted, as Adobe applications write it. Baseline and progressive 8-bit JPEGs only.
/// </summary>
internal static class JpegInfo
{
    public static bool IsJpeg(ReadOnlySpan<byte> bytes) => bytes.Length > 3 && bytes[0] == 0xFF && bytes[1] == 0xD8 && bytes[2] == 0xFF;

    public static (int Width, int Height, int Components, bool Inverted) Read(byte[] jpeg)
    {
        var adobe = false;
        for (var at = 2; at + 4 <= jpeg.Length;)
        {
            if (jpeg[at] != 0xFF)
            {
                throw new InvalidDataException("The JPEG file is damaged.");
            }

            var marker = jpeg[at + 1];
            if (marker is 0xFF or 0x01 or (>= 0xD0 and <= 0xD7))
            {
                at += marker == 0xFF ? 1 : 2;
                continue;
            }

            var length = BinaryPrimitives.ReadUInt16BigEndian(jpeg.AsSpan(at + 2));
            if (length < 2 || at + 2 + length > jpeg.Length)
            {
                throw new InvalidDataException("The JPEG file is cut short.");
            }

            adobe |= marker == 0xEE && length >= 7 && jpeg.AsSpan(at + 4, 5).SequenceEqual("Adobe"u8);
            if (marker is 0xC0 or 0xC1 or 0xC2)
            {
                return Frame(jpeg.AsSpan(at + 4, length - 2), adobe);
            }

            if (marker is (>= 0xC3 and <= 0xCF) and not (0xC4 or 0xC8 or 0xCC))
            {
                throw new InvalidDataException("The JPEG file is lossless, hierarchical or arithmetic-coded.");
            }

            at += 2 + length;
        }

        throw new InvalidDataException("The JPEG file has no frame.");
    }

    // A start-of-frame segment: precision, height, width, components.
    private static (int, int, int, bool) Frame(ReadOnlySpan<byte> frame, bool adobe)
    {
        var (precision, height, width, components) = (frame[0], BinaryPrimitives.ReadUInt16BigEndian(frame[1..]), BinaryPrimitives.ReadUInt16BigEndian(frame[3..]), frame[5]);
        if (precision != 8 || width == 0 || height == 0 || components is not (1 or 3 or 4))
        {
            throw new InvalidDataException("The JPEG file is not an 8-bit grey, colour or CMYK picture.");
        }

        return (width, height, components, components == 4 && adobe);
    }
}
