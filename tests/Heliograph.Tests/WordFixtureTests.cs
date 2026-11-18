using System.IO.Compression;
using System.Text;
using Heliograph.Pdf;
using Heliograph.Tests.Support;

namespace Heliograph.Tests;

/// <summary>
/// Documents saved by Microsoft 365 with the PDF Word itself exported from them, the check on LibreOffice as the reference
/// (docs/pdf-engine.md). Fixtures/Word/inputs/new-document.docx was made from a blank document in Word for the web; its text says
/// how. A new Word document leaves out doNotUseHTMLParagraphAutoSpacing, and Word then puts the larger of a paragraph's
/// space after and the next one's space before between them, not their sum.
/// </summary>
public class WordFixtureTests
{
    // Above the heading: 8pt after the paragraph before it, 18pt before the heading (Word's Heading 1).
    private const double Larger = 18;
    private const double Sum = 26;

    [PdfLinesFact]
    public void Word_puts_the_larger_of_two_paragraphs_spacing_between_them_in_a_new_document() =>
        GapAboveHeading(File.ReadAllBytes(Fixture("new-document.word.pdf"))).Should().BeApproximately(Larger, 1);

    [PdfLinesFact]
    public async Task The_built_in_converter_spaces_paragraphs_as_Word_does_in_a_new_document()
    {
        var pdf = await new HeliographPdfConverter().ConvertAsync(File.ReadAllBytes(Fixture(Path.Combine("inputs", "new-document.docx"))));

        GapAboveHeading(pdf).Should().BeApproximately(Larger, 1);
    }

    [PdfLinesFact]
    public async Task With_doNotUseHTMLParagraphAutoSpacing_the_two_spacings_are_added()
    {
        var docx = WithSetting(File.ReadAllBytes(Fixture(Path.Combine("inputs", "new-document.docx"))), "<w:doNotUseHTMLParagraphAutoSpacing />");

        GapAboveHeading(await new HeliographPdfConverter().ConvertAsync(docx)).Should().BeApproximately(Sum, 1);
    }

    private static string Fixture(string name) => Path.Combine(AppContext.BaseDirectory, "Fixtures", "Word", name);

    // The space between the foot of the line above the heading and the heading's top. A body line's height is the
    // distance between the first paragraph's first two lines.
    private static double GapAboveHeading(byte[] pdf)
    {
        var lines = PdfLines.Read(pdf);
        var heading = lines.FindIndex(l => l.Text.StartsWith("This is a heading 1", StringComparison.Ordinal));
        heading.Should().BeGreaterThan(1, "the heading follows two paragraphs");
        var lineHeight = lines[1].Top - lines[0].Top;
        return lines[heading].Top - (lines[heading - 1].Top + lineHeight);
    }

    private static byte[] WithSetting(byte[] docx, string setting)
    {
        using var stream = new MemoryStream();
        stream.Write(docx);
        using (var zip = new ZipArchive(stream, ZipArchiveMode.Update, leaveOpen: true))
        {
            var entry = zip.GetEntry("word/settings.xml")!;
            string xml;
            using (var reader = new StreamReader(entry.Open()))
            {
                xml = reader.ReadToEnd();
            }

            entry.Delete();
            using var writer = new StreamWriter(zip.CreateEntry("word/settings.xml").Open(), new UTF8Encoding(false));
            writer.Write(xml.Replace("<w:compat>", "<w:compat>" + setting, StringComparison.Ordinal));
        }

        return stream.ToArray();
    }
}
