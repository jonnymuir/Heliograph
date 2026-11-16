namespace Heliograph.Documents;

/// <summary>
/// Turns a rendered document into a PDF. This is a seam: the core library never converts anything itself, so a host
/// chooses its route to PDF, or none. Heliograph.Pdf.LibreOffice is one implementation.
/// </summary>
public interface IPdfConverter
{
    /// <summary>Converts a rendered .docx to a PDF.</summary>
    /// <exception cref="PdfConversionException">The document could not be converted.</exception>
    Task<byte[]> ConvertAsync(byte[] docx, CancellationToken cancellationToken = default);
}

/// <summary>A document could not be converted to PDF. The message never includes the document's content.</summary>
public sealed class PdfConversionException : Exception
{
    public PdfConversionException()
    {
    }

    public PdfConversionException(string message)
        : base(message)
    {
    }

    public PdfConversionException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
