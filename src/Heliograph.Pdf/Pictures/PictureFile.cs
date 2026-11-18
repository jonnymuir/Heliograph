namespace Heliograph.Pdf.Pictures;

/// <summary>
/// A picture's file as the converter takes it: a JPEG, which a PDF holds as it is, or a PNG, which is decoded. Anything
/// else, or a file too large or not readable, is refused at compile (<see cref="Problem"/>).
/// </summary>
internal sealed record PictureFile(byte[] Bytes, bool IsJpeg, int Width, int Height, int Components, bool Inverted)
{
    /// <summary>What is wrong with a picture's file for this converter, in words for an author; null when it can be drawn.</summary>
    public static string? Problem(byte[]? bytes)
    {
        if (bytes is null)
        {
            return "a picture whose file is missing";
        }

        try
        {
            var file = Read(bytes);
            if ((long)file.Width * file.Height > PngInfo.MaxPixels)
            {
                return "a picture too large to draw safely (more than 25 million pixels)";
            }

            // A PNG is decoded in full here, so a damaged one is found at compile, never when a letter is made.
            if (!file.IsJpeg)
            {
                PngDecoder.Decode(bytes);
            }

            return null;
        }
        catch (InvalidDataException)
        {
            return JpegInfo.IsJpeg(bytes) || PngDecoder.IsPng(bytes) ? "a picture whose file is damaged or uses a kind of JPEG or PNG it cannot read" : "a picture that is not a PNG or JPEG file";
        }
    }

    public static PictureFile Read(byte[] bytes)
    {
        if (JpegInfo.IsJpeg(bytes))
        {
            var (width, height, components, inverted) = JpegInfo.Read(bytes);
            return new PictureFile(bytes, true, width, height, components, inverted);
        }

        var (w, h) = PngDecoder.Size(bytes);
        return new PictureFile(bytes, false, w, h, 0, false);
    }
}
