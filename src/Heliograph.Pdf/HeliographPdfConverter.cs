using Heliograph.Documents;
using Heliograph.Pdf.Layout;
using Heliograph.Pdf.Reading;
using Heliograph.Pdf.Writing;

namespace Heliograph.Pdf;

/// <summary>Limits on one conversion, so a strange letter can never hold a server.</summary>
public sealed record HeliographPdfOptions
{
    /// <summary>The most pages a letter may run to. A letter that runs past it is refused. 500 by default.</summary>
    public int MaxPages { get; init; } = 500;
}

/// <summary>
/// Converts a rendered letter to an accessible, tagged PDF (PDF/UA-1) in .NET alone: nothing to install and no program
/// started. The same letter always gives the same bytes. Fonts the letter names are drawn in built-in look-alikes with
/// the same widths (Liberation Sans for Arial, Liberation Serif for Times New Roman, Liberation Mono for Courier New).
/// </summary>
/// <remarks>
/// It is also an <see cref="IDocumentCheck"/>: registered as a host's converter, it says when a template is compiled
/// what it cannot lay out yet (HG4 codes), so no PDF is ever quietly missing something.
/// </remarks>
public sealed class HeliographPdfConverter(HeliographPdfOptions? options = null) : IPdfConverter, IDocumentCheck
{
    private readonly HeliographPdfOptions _options = options ?? new HeliographPdfOptions();

    /// <summary>What in a template this converter cannot lay out yet, each with its place and a fix.</summary>
    public IReadOnlyList<Diagnostic> Check(DocumentToCheck document)
    {
        ArgumentNullException.ThrowIfNull(document);
        return Checking.LayoutLimits.Check(document);
    }

    public Task<byte[]> ConvertAsync(byte[] docx, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(docx);
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            var document = WordReader.Read(docx);
            var (pages, root) = PageLayout.Run(document, _options.MaxPages, cancellationToken);
            return Task.FromResult(DocumentWriter.Write(document, pages, root));
        }
        catch (Exception e) when (e is PageLimitException or UndrawableCharacterException)
        {
            // A fault in one recipient's data, such as a name in a script no built-in font has: said without quoting it.
            throw new PdfConversionException(e.Message, e);
        }
        catch (Exception e) when (e is InvalidDataException or System.Xml.XmlException or ArgumentException or IndexOutOfRangeException or FormatException)
        {
            // Never the document's content in the message: it carries personal data.
            throw new PdfConversionException("The letter could not be read as a Word document.", e);
        }
    }
}
