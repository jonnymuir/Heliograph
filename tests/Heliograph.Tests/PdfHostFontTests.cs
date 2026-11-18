using System.Text;
using System.Text.Json;
using Heliograph.Documents;
using Heliograph.Pdf;
using Heliograph.Tests.Support;

namespace Heliograph.Tests;

/// <summary>
/// Fonts a host gives Heliograph's own PDF converter: drawn wherever a letter names their family, bold and italic from
/// their own files or else the regular face, no longer pointed out as drawn in a look-alike, and refused when the font
/// may not be embedded. The tests' own font is Liberation Sans with its names changed to Heliograph Sans.
/// </summary>
public class PdfHostFontTests
{
    private const string W = "xmlns:w=\"http://schemas.openxmlformats.org/wordprocessingml/2006/main\"";
    private static readonly FieldDictionary NoFields = new([]);

    [PdfLinesFact]
    public async Task A_letter_in_the_hosts_font_is_drawn_in_it()
    {
        var converter = new HeliographPdfConverter(new HeliographPdfOptions { Fonts = [House("Regular"), House("Bold")] });
        var pdf = Encoding.Latin1.GetString(await converter.ConvertAsync(Rendered(Letter(), converter)));

        pdf.Should().Contain("+HeliographSans ").And.Contain("+HeliographSans-Bold ");
        pdf.Should().NotContain("+LiberationSans");
    }

    [PdfLinesFact]
    public async Task A_style_the_host_gives_no_file_for_is_drawn_in_the_regular_face()
    {
        var converter = new HeliographPdfConverter(new HeliographPdfOptions { Fonts = [House("Regular")] });
        var pdf = Encoding.Latin1.GetString(await converter.ConvertAsync(Rendered(Letter(), converter)));

        pdf.Should().Contain("+HeliographSans ").And.NotContain("+HeliographSans-Bold");
    }

    [Fact]
    public void The_hosts_font_is_not_pointed_out_as_drawn_in_a_look_alike()
    {
        static IEnumerable<Diagnostic> Warnings(HeliographPdfConverter converter) =>
            DocumentTemplate.Compile(Letter(), NoFields, [converter]).Diagnostics.Where(d => d.Code == DiagnosticCodes.PdfFontDrawnInLookAlike);

        Warnings(new HeliographPdfConverter()).Should().ContainSingle();
        Warnings(new HeliographPdfConverter(new HeliographPdfOptions { Fonts = [House("Regular")] })).Should().BeEmpty();
    }

    [VeraPdfTheory]
    [InlineData("Regular")]
    public async Task A_letter_in_the_hosts_font_is_still_an_accessible_PDF(string face)
    {
        var converter = new HeliographPdfConverter(new HeliographPdfOptions { Fonts = [House(face)] });

        PdfValidator.FailuresAsPdfUa(await converter.ConvertAsync(Rendered(Letter(), converter))).Should().BeEmpty();
    }

    [Fact]
    public void A_font_that_may_not_be_embedded_is_refused()
    {
        var restricted = House("Regular");
        var os2 = Table(restricted, "OS/2");
        (restricted[os2 + 8], restricted[os2 + 9]) = (0, 2);

        var making = () => new HeliographPdfConverter(new HeliographPdfOptions { Fonts = [restricted] });

        making.Should().Throw<ArgumentException>().WithMessage("*does not allow embedding*");
    }

    [Fact]
    public void Something_that_is_not_a_font_is_refused()
    {
        var making = () => new HeliographPdfConverter(new HeliographPdfOptions { Fonts = [Encoding.ASCII.GetBytes("not a font at all, just some text")] });

        making.Should().Throw<ArgumentException>().WithMessage("*not a font file*");
    }

    // Liberation Sans with its family and PostScript names changed, at the same lengths so no offset moves.
    private static byte[] House(string face)
    {
        var bytes = File.ReadAllBytes(Path.Combine(BuiltIn(), $"LiberationSans-{face}.ttf"));
        foreach (var (from, to) in new[] { ("Liberation Sans", "Heliograph Sans"), ("LiberationSans", "HeliographSans") })
        {
            foreach (var encoding in new Encoding[] { Encoding.ASCII, Encoding.BigEndianUnicode })
            {
                Replace(bytes, encoding.GetBytes(from), encoding.GetBytes(to));
            }
        }

        return bytes;
    }

    private static void Replace(byte[] bytes, byte[] from, byte[] to)
    {
        for (var i = 0; i <= bytes.Length - from.Length; i++)
        {
            if (bytes.AsSpan(i, from.Length).SequenceEqual(from))
            {
                to.CopyTo(bytes, i);
            }
        }
    }

    // Where a table starts in a font file, from its table directory.
    private static int Table(byte[] font, string tag)
    {
        var tables = (font[4] << 8) | font[5];
        for (var i = 0; i < tables; i++)
        {
            var at = 12 + (i * 16);
            if (Encoding.ASCII.GetString(font, at, 4) == tag)
            {
                return (font[at + 8] << 24) | (font[at + 9] << 16) | (font[at + 10] << 8) | font[at + 11];
            }
        }

        throw new InvalidDataException(tag);
    }

    // The built-in fonts, in the source next to the tests.
    private static string BuiltIn()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            var fonts = Path.Combine(dir.FullName, "src", "Heliograph.Pdf", "Fonts", "BuiltIn");
            if (Directory.Exists(fonts))
            {
                return fonts;
            }
        }

        throw new DirectoryNotFoundException("The built-in fonts were not found.");
    }

    private static MemoryStream Letter()
    {
        var styles = $"<w:styles {W}><w:docDefaults><w:rPrDefault><w:rPr><w:rFonts w:ascii=\"Heliograph Sans\" w:hAnsi=\"Heliograph Sans\"/><w:sz w:val=\"22\"/></w:rPr></w:rPrDefault></w:docDefaults></w:styles>";
        return new TestDocx().Part("word/styles.xml", styles, "application/vnd.openxmlformats-officedocument.wordprocessingml.styles+xml")
            .Relationship("http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles", "styles.xml", external: false)
            .RawParagraph($"<w:p {W}><w:r><w:t xml:space=\"preserve\">Dear Ms Example, </w:t></w:r><w:r><w:rPr><w:b/></w:rPr><w:t>welcome</w:t></w:r></w:p>").Build();
    }

    private static byte[] Rendered(MemoryStream template, HeliographPdfConverter converter)
    {
        var compilation = DocumentTemplate.Compile(template, NoFields, [converter]);
        compilation.Succeeded.Should().BeTrue();
        return compilation.Document!.Render(JsonDocument.Parse("{}").RootElement, Benchmark.British).Docx!;
    }
}
