using System.Diagnostics;
using Heliograph.Documents;

namespace Heliograph.Pdf.LibreOffice;

/// <summary>How the converter finds and runs LibreOffice.</summary>
public sealed class LibreOfficeOptions
{
    /// <summary>The soffice executable. When null, the usual install places are tried.</summary>
    public string? ExecutablePath { get; init; }

    /// <summary>How long one conversion may take before LibreOffice is stopped.</summary>
    public TimeSpan Timeout { get; init; } = TimeSpan.FromMinutes(2);

    /// <summary>
    /// Whether to ask for PDF/UA, the standard for accessible PDF: a tagged file that says its language. On by
    /// default, because a letter a screen reader cannot follow is not finished.
    /// </summary>
    public bool Accessible { get; init; } = true;
}

/// <summary>
/// Converts a rendered document to PDF by running a LibreOffice installed on this machine, with no window. Each
/// conversion has a working folder and a LibreOffice profile of its own, removed afterwards, so conversions can run
/// side by side and leave nothing behind.
/// </summary>
public sealed class LibreOfficePdfConverter(LibreOfficeOptions? options = null) : IPdfConverter
{
    private const string AccessibleExport =
        "pdf:writer_pdf_Export:{\"PDFUACompliance\":{\"type\":\"boolean\",\"value\":\"true\"},\"UseTaggedPDF\":{\"type\":\"boolean\",\"value\":\"true\"}}";

    private static readonly byte[] ZipSignature = [0x50, 0x4B, 0x03, 0x04];

    private static readonly string[] UsualPlaces =
    [
        "/Applications/LibreOffice.app/Contents/MacOS/soffice",
        "/usr/bin/soffice",
        "/usr/local/bin/soffice",
        "/opt/homebrew/bin/soffice",
        @"C:\Program Files\LibreOffice\program\soffice.exe",
    ];

    private readonly LibreOfficeOptions _options = options ?? new LibreOfficeOptions();

    /// <summary>The LibreOffice that will be run, or null if none can be found.</summary>
    public string? Executable => _options.ExecutablePath ?? UsualPlaces.FirstOrDefault(File.Exists);

    public async Task<byte[]> ConvertAsync(byte[] docx, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(docx);
        if (!docx.AsSpan().StartsWith(ZipSignature))
        {
            // LibreOffice would turn any text into a PDF. Only a Word document, which is a zip, is a letter.
            throw new PdfConversionException("This is not a Word document (.docx), so it was not converted.");
        }

        var executable = Executable ?? throw new PdfConversionException(
            "LibreOffice was not found. Install it, or set LibreOfficeOptions.ExecutablePath to the soffice executable.");

        var work = Directory.CreateTempSubdirectory("heliograph-pdf-");
        try
        {
            await File.WriteAllBytesAsync(Path.Combine(work.FullName, "letter.docx"), docx, cancellationToken).ConfigureAwait(false);
            await RunAsync(executable, work.FullName, cancellationToken).ConfigureAwait(false);
            var pdf = Path.Combine(work.FullName, "letter.pdf");
            return File.Exists(pdf)
                ? await File.ReadAllBytesAsync(pdf, cancellationToken).ConfigureAwait(false)
                : throw new PdfConversionException("LibreOffice finished without producing a PDF.");
        }
        finally
        {
            // The folder holds a letter, which is personal data. It is removed whatever happened.
            work.Delete(recursive: true);
        }
    }

    private static Process Start(ProcessStartInfo start)
    {
        try
        {
            return Process.Start(start) ?? throw new PdfConversionException("LibreOffice could not be started.");
        }
        catch (System.ComponentModel.Win32Exception e)
        {
            throw new PdfConversionException($"LibreOffice could not be started from {start.FileName}.", e);
        }
    }

    private async Task RunAsync(string executable, string folder, CancellationToken cancellationToken)
    {
        var start = new ProcessStartInfo(executable)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            WorkingDirectory = folder,
        };
        string[] arguments =
        [
            $"-env:UserInstallation={new Uri(Path.Combine(folder, "profile")).AbsoluteUri}",
            "--headless", "--norestore", "--nolockcheck",
            "--convert-to", _options.Accessible ? AccessibleExport : "pdf",
            "--outdir", folder, Path.Combine(folder, "letter.docx"),
        ];
        foreach (var argument in arguments)
        {
            start.ArgumentList.Add(argument);
        }

        using var process = Start(start);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(_options.Timeout);
        try
        {
            await process.WaitForExitAsync(timeout.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            process.Kill(entireProcessTree: true);
            cancellationToken.ThrowIfCancellationRequested();
            throw new PdfConversionException($"LibreOffice did not finish within {_options.Timeout.TotalSeconds:0} seconds and was stopped.");
        }

        if (process.ExitCode != 0)
        {
            // What LibreOffice printed is not passed on: it can quote the document.
            throw new PdfConversionException($"LibreOffice exited with code {process.ExitCode}.");
        }
    }
}
