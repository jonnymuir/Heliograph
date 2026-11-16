using System.Diagnostics;

namespace Heliograph.Tests.Support;

/// <summary>
/// An independent judge of a PDF: veraPDF, the reference validator for PDF/UA, run in a container with no network.
/// It shares no code with LibreOffice, which made the file.
/// </summary>
internal static class PdfValidator
{
    // veraPDF 1.30.4, pinned by digest so the judge cannot change unnoticed.
    private const string Image = "verapdf/cli@sha256:f085855d442ad415e79fabbabcdd1803c2db4ee097cd5b94506c95ecdcfa1728";

    private static readonly Lazy<bool> DockerRuns = new(() =>
    {
        try
        {
            return Run(["info"], TimeSpan.FromSeconds(30)).ExitCode == 0;
        }
        catch (System.ComponentModel.Win32Exception)
        {
            return false;
        }
    });

    public static bool Available => DockerRuns.Value;

    /// <summary>What veraPDF says is wrong with the file as PDF/UA-1, or nothing when it complies.</summary>
    public static string FailuresAsPdfUa(byte[] pdf)
    {
        var work = Directory.CreateTempSubdirectory("heliograph-verapdf-");
        try
        {
            var path = Path.Combine(work.FullName, "letter.pdf");
            File.WriteAllBytes(path, pdf);
            if (!OperatingSystem.IsWindows())
            {
                // The container runs as a user of its own, which must be able to read the file.
                File.SetUnixFileMode(work.FullName, (UnixFileMode)Convert.ToInt32("755", 8));
                File.SetUnixFileMode(path, (UnixFileMode)Convert.ToInt32("644", 8));
            }

            var (exitCode, output) = Run(
                ["run", "--rm", "--network", "none", "-v", $"{work.FullName}:/data:ro", Image, "--flavour", "ua1", "--format", "text", "-v", "/data/letter.pdf"],
                TimeSpan.FromMinutes(5));
            return output.Contains("PASS /data/letter.pdf", StringComparison.Ordinal) ? "" : $"veraPDF exited with code {exitCode}: {output}";
        }
        finally
        {
            work.Delete(recursive: true);
        }
    }

    private static (int ExitCode, string Output) Run(string[] arguments, TimeSpan limit)
    {
        var start = new ProcessStartInfo("docker") { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };
        foreach (var argument in arguments)
        {
            start.ArgumentList.Add(argument);
        }

        using var process = Process.Start(start)!;
        var output = process.StandardOutput.ReadToEndAsync();
        var error = process.StandardError.ReadToEndAsync();
        if (!process.WaitForExit(limit))
        {
            process.Kill(entireProcessTree: true);
            throw new TimeoutException($"docker {arguments[0]} did not finish within {limit.TotalSeconds:0} seconds.");
        }

        return (process.ExitCode, output.Result + error.Result);
    }
}

/// <summary>
/// A test that needs LibreOffice and the PDF validator, which runs in Docker. Skipped where either is missing,
/// unless they are required there.
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
internal sealed class ValidatedPdfTheoryAttribute : TheoryAttribute
{
    public ValidatedPdfTheoryAttribute()
    {
        if ((LibreOffice.Executable is null || !PdfValidator.Available) && !LibreOffice.Required)
        {
            Skip = "This needs LibreOffice, and Docker to run the PDF validator.";
        }
    }
}
