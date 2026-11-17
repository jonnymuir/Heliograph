using System.Diagnostics;
using System.Globalization;
using System.Xml.Linq;

namespace Heliograph.Tests.Support;

/// <summary>A line of text as a PDF reader finds it: its page, where it sits, and its words.</summary>
internal sealed record PdfLine(int Page, double Top, double Left, string Text);

/// <summary>
/// Reads where every line of a PDF sits, using Poppler's pdftotext, a separate program run only by the tests. It shares
/// no code with either converter, so it judges both the same way.
/// </summary>
internal static class PdfLines
{
    private static readonly string[] Candidates = ["/opt/homebrew/bin/pdftotext", "/usr/bin/pdftotext", "/usr/local/bin/pdftotext"];

    public static string? Executable => Candidates.FirstOrDefault(File.Exists);

    public static List<PdfLine> Read(byte[] pdf)
    {
        var work = Directory.CreateTempSubdirectory("heliograph-lines-");
        try
        {
            var path = Path.Combine(work.FullName, "letter.pdf");
            File.WriteAllBytes(path, pdf);
            var start = new ProcessStartInfo(Executable ?? throw new InvalidOperationException("pdftotext (Poppler) was not found."))
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
            };
            foreach (var argument in new[] { "-bbox-layout", "-enc", "UTF-8", path, "-" })
            {
                start.ArgumentList.Add(argument);
            }

            using var process = Process.Start(start)!;
            var output = process.StandardOutput.ReadToEnd();
            process.WaitForExit(60_000);
            return Parse(output);
        }
        finally
        {
            work.Delete(recursive: true);
        }
    }

    private static List<PdfLine> Parse(string xhtml)
    {
        XNamespace x = "http://www.w3.org/1999/xhtml";
        var lines = new List<PdfLine>();
        var page = 0;
        foreach (var p in XDocument.Parse(xhtml).Descendants(x + "page"))
        {
            page++;
            foreach (var line in p.Descendants(x + "line"))
            {
                var words = line.Elements(x + "word").Select(w => w.Value).ToList();
                lines.Add(new PdfLine(page, Number(line, "yMin"), Number(line, "xMin"), string.Join(' ', words)));
            }
        }

        return [.. lines.OrderBy(l => l.Page).ThenBy(l => Math.Round(l.Top)).ThenBy(l => l.Left)];
    }

    private static double Number(XElement e, string name) => double.Parse((string)e.Attribute(name)!, CultureInfo.InvariantCulture);
}

/// <summary>A test that compares the built-in converter with LibreOffice, line by line. Needs LibreOffice and Poppler.</summary>
[AttributeUsage(AttributeTargets.Method)]
internal sealed class ComparedPdfTheoryAttribute : TheoryAttribute
{
    public ComparedPdfTheoryAttribute()
    {
        if ((LibreOffice.Executable is null || PdfLines.Executable is null) && !LibreOffice.Required)
        {
            Skip = "This needs LibreOffice, and Poppler's pdftotext to read where lines sit.";
        }
    }
}

/// <summary>A test that needs only the PDF validator, which runs in Docker.</summary>
[AttributeUsage(AttributeTargets.Method)]
internal sealed class VeraPdfTheoryAttribute : TheoryAttribute
{
    public VeraPdfTheoryAttribute()
    {
        if (!PdfValidator.Available && !LibreOffice.Required)
        {
            Skip = "This needs Docker to run the PDF validator.";
        }
    }
}
