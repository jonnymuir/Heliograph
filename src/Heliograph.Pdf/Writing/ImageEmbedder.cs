using System.Security.Cryptography;
using Heliograph.Pdf.Pictures;
using static Heliograph.Pdf.Writing.PdfObjects;

namespace Heliograph.Pdf.Writing;

/// <summary>
/// The pictures a PDF draws, each written once however often it appears: a JPEG as it is, a PNG decoded and compressed
/// again, with its transparency as a soft mask.
/// </summary>
internal sealed class ImageEmbedder(PdfObjects objects)
{
    private readonly Dictionary<string, (string Name, int Id)> _images = new(StringComparer.Ordinal);

    public (string Name, int Id) Use(PictureFile file)
    {
        var key = Convert.ToHexString(SHA256.HashData(file.Bytes));
        if (!_images.TryGetValue(key, out var image))
        {
            _images[key] = image = (Invariant($"Im{_images.Count + 1}"), file.IsJpeg ? Jpeg(file) : Png(file));
        }

        return image;
    }

    private int Jpeg(PictureFile file)
    {
        var space = file.Components switch { 1 => "/DeviceGray", 4 => "/DeviceCMYK", _ => "/DeviceRGB" };
        var decode = file.Inverted ? " /Decode [1 0 1 0 1 0 1 0]" : "";
        return objects.Stream(file.Bytes,
            Invariant($" /Type /XObject /Subtype /Image /Width {file.Width} /Height {file.Height} /ColorSpace {space} /BitsPerComponent 8{decode} /Filter /DCTDecode"),
            compress: false);
    }

    private int Png(PictureFile file)
    {
        var raster = PngDecoder.Decode(file.Bytes);
        var mask = raster.Alpha is { } alpha
            ? Invariant($" /SMask {objects.Stream(alpha, Invariant($" /Type /XObject /Subtype /Image /Width {raster.Width} /Height {raster.Height} /ColorSpace /DeviceGray /BitsPerComponent 8"))} 0 R")
            : "";
        var space = raster.Channels == 1 ? "/DeviceGray" : "/DeviceRGB";
        return objects.Stream(raster.Color,
            Invariant($" /Type /XObject /Subtype /Image /Width {raster.Width} /Height {raster.Height} /ColorSpace {space} /BitsPerComponent 8{mask}"));
    }
}
