using System.Diagnostics;
using System.Text;

namespace Heliograph.Tests.Support;

/// <summary>
/// The first page of a PDF drawn as pixels by Poppler's pdftoppm, a separate program run only by the tests, at 72 dots an
/// inch so a point is a pixel. It shares no code with the converter.
/// </summary>
internal sealed class PdfRender
{
    private readonly byte[] _rgb;

    private PdfRender(int width, int height, byte[] rgb) => (Width, Height, _rgb) = (width, height, rgb);

    public int Width { get; }

    public int Height { get; }

    public static PdfRender FirstPage(byte[] pdf)
    {
        var work = Directory.CreateTempSubdirectory("heliograph-render-");
        try
        {
            var path = Path.Combine(work.FullName, "letter.pdf");
            File.WriteAllBytes(path, pdf);
            var executable = Path.Combine(Path.GetDirectoryName(PdfLines.Executable)!, "pdftoppm");
            var start = new ProcessStartInfo(executable) { UseShellExecute = false, RedirectStandardError = true };
            foreach (var argument in new[] { "-r", "72", "-f", "1", "-l", "1", "-singlefile", path, Path.Combine(work.FullName, "page") })
            {
                start.ArgumentList.Add(argument);
            }

            using (var process = Process.Start(start)!)
            {
                process.WaitForExit(60_000);
            }

            return Parse(File.ReadAllBytes(Path.Combine(work.FullName, "page.ppm")));
        }
        finally
        {
            work.Delete(recursive: true);
        }
    }

    /// <summary>The colour at a point, measured from the page's top left.</summary>
    public (int R, int G, int B) At(double x, double y)
    {
        var at = (((int)y * Width) + (int)x) * 3;
        return (_rgb[at], _rgb[at + 1], _rgb[at + 2]);
    }

    // A binary PPM: "P6", width, height, 255, then RGB bytes.
    private static PdfRender Parse(byte[] ppm)
    {
        var fields = new List<string>();
        var at = 0;
        while (fields.Count < 4)
        {
            while (char.IsWhiteSpace((char)ppm[at]))
            {
                at++;
            }

            var start = at;
            while (!char.IsWhiteSpace((char)ppm[at]))
            {
                at++;
            }

            fields.Add(Encoding.ASCII.GetString(ppm, start, at - start));
        }

        var (width, height) = (int.Parse(fields[1], System.Globalization.CultureInfo.InvariantCulture), int.Parse(fields[2], System.Globalization.CultureInfo.InvariantCulture));
        return new PdfRender(width, height, ppm[(at + 1)..]);
    }
}
