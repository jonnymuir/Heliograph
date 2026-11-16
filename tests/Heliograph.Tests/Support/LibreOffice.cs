using System.Diagnostics;

namespace Heliograph.Tests.Support;

/// <summary>Drives a real LibreOffice with no window, so a test can put a file through an editor's own open and save.</summary>
internal static class LibreOffice
{
    private static readonly string[] Candidates =
    [
        "/Applications/LibreOffice.app/Contents/MacOS/soffice",
        "/usr/bin/soffice",
        "/usr/local/bin/soffice",
        "/opt/homebrew/bin/soffice",
        @"C:\Program Files\LibreOffice\program\soffice.exe",
    ];

    /// <summary>Set to a path to choose the LibreOffice to drive.</summary>
    public const string PathVariable = "HELIOGRAPH_SOFFICE";

    /// <summary>Set to 1 where LibreOffice must be present (CI), so its absence fails instead of skipping.</summary>
    public const string RequiredVariable = "HELIOGRAPH_REQUIRE_EDITOR";

    public static string? Executable =>
        Environment.GetEnvironmentVariable(PathVariable) is { Length: > 0 } chosen ? chosen : Candidates.FirstOrDefault(File.Exists);

    public static bool Required => Environment.GetEnvironmentVariable(RequiredVariable) == "1";

    /// <summary>Opens a .docx held in memory in LibreOffice and saves it again. It throws if LibreOffice cannot read it.</summary>
    public static byte[] OpenAndSave(byte[] docx)
    {
        var work = Directory.CreateTempSubdirectory("heliograph-rendered-");
        try
        {
            var path = Path.Combine(work.FullName, "rendered.docx");
            File.WriteAllBytes(path, docx);
            return OpenAndSave(path);
        }
        finally
        {
            work.Delete(recursive: true);
        }
    }

    /// <summary>Opens the file in LibreOffice and saves it again as .docx, returning what LibreOffice wrote.</summary>
    public static byte[] OpenAndSave(string docxPath)
    {
        var executable = Executable ?? throw new InvalidOperationException(
            $"LibreOffice was not found. Install it, or set {PathVariable} to the soffice executable.");
        var work = Directory.CreateTempSubdirectory("heliograph-editor-");
        try
        {
            var profile = new Uri(Path.Combine(work.FullName, "profile")).AbsoluteUri;
            var output = Path.Combine(work.FullName, "out");
            var start = new ProcessStartInfo(executable)
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
            };
            foreach (var argument in new[]
                     {
                         $"-env:UserInstallation={profile}", "--headless", "--norestore",
                         "--convert-to", "docx:MS Word 2007 XML", "--outdir", output, docxPath,
                     })
            {
                start.ArgumentList.Add(argument);
            }

            using var process = Process.Start(start)!;
            if (!process.WaitForExit(TimeSpan.FromMinutes(3)))
            {
                process.Kill(entireProcessTree: true);
                throw new TimeoutException("LibreOffice did not finish saving the document within 3 minutes.");
            }

            var saved = Path.Combine(output, Path.GetFileName(docxPath));
            if (!File.Exists(saved))
            {
                throw new InvalidOperationException(
                    $"LibreOffice exited with code {process.ExitCode} and saved nothing: {process.StandardError.ReadToEnd()}");
            }

            return File.ReadAllBytes(saved);
        }
        finally
        {
            work.Delete(recursive: true);
        }
    }
}

/// <summary>A test that needs LibreOffice. Skipped where it is not installed, unless it is required there.</summary>
[AttributeUsage(AttributeTargets.Method)]
internal sealed class EditorFactAttribute : FactAttribute
{
    public EditorFactAttribute()
    {
        if (LibreOffice.Executable is null && !LibreOffice.Required)
        {
            Skip = $"LibreOffice is not installed. Set {LibreOffice.PathVariable} to run this test.";
        }
    }
}
