using System.Text;
using System.Text.Json;
using Heliograph.Documents;
using Heliograph.Pdf;
using Heliograph.Pdf.LibreOffice;
using Heliograph.Tests.Support;

namespace Heliograph.Tests;

/// <summary>
/// Pictures set in line with the text, drawn by Heliograph's own PDF converter: every kind of PNG with its transparency,
/// JPEGs as they are, sized and placed as LibreOffice places them, and tagged for a screen reader by their description.
/// </summary>
public class PdfPictureTests
{
    private const string W = "xmlns:w=\"http://schemas.openxmlformats.org/wordprocessingml/2006/main\"";
    private const string Drawing = "xmlns:wp=\"http://schemas.openxmlformats.org/drawingml/2006/wordprocessingDrawing\" "
        + "xmlns:a=\"http://schemas.openxmlformats.org/drawingml/2006/main\" xmlns:pic=\"http://schemas.openxmlformats.org/drawingml/2006/picture\" "
        + "xmlns:r=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships\"";
    private const string ImageRelationship = "http://schemas.openxmlformats.org/officeDocument/2006/relationships/image";
    private const int Margin = 72;
    private static readonly FieldDictionary NoFields = new([]);

    // Arial at 11pt with no space between paragraphs, so both converters set the same text.
    private static readonly string Styles = $"<w:styles {W}><w:docDefaults><w:rPrDefault><w:rPr><w:rFonts w:ascii=\"Arial\" w:hAnsi=\"Arial\"/><w:sz w:val=\"22\"/></w:rPr></w:rPrDefault>"
        + "<w:pPrDefault><w:pPr><w:spacing w:after=\"0\"/></w:pPr></w:pPrDefault></w:docDefaults><w:style w:type=\"paragraph\" w:default=\"1\" w:styleId=\"Normal\"><w:name w:val=\"Normal\"/></w:style></w:styles>";

    private static readonly (int R, int G, int B) Red = (255, 0, 0), Green = (0, 255, 0), Blue = (0, 0, 255), Black = (0, 0, 0), White = (255, 255, 255);

    // Two by two pixels, each drawn 36pt square: top left, top right, bottom left, bottom right.
    public static TheoryData<string, byte[], (int, int, int)[]> Pngs => new()
    {
        { "colour, 8 bits", Png(2, 8, [255, 0, 0], [0, 255, 0], [0, 0, 255], [0, 0, 0]), [Red, Green, Blue, Black] },
        { "colour, 16 bits", Png(2, 16, [65535, 0, 0], [0, 65535, 0], [0, 0, 65535], [0, 0, 0]), [Red, Green, Blue, Black] },
        { "colour, interlaced", Png(2, 8, [255, 0, 0], [0, 255, 0], [0, 0, 255], [0, 0, 0], interlaced: true), [Red, Green, Blue, Black] },
        { "grey, 1 bit", Png(0, 1, [0], [1], [1], [0]), [Black, White, White, Black] },
        { "grey, 4 bits", Png(0, 4, [15], [0], [0], [15]), [White, Black, Black, White] },
        { "grey, 16 bits", Png(0, 16, [0], [65535], [65535], [0]), [Black, White, White, Black] },
        { "palette, 2 bits, first colour see-through", Png(3, 2, [0], [1], [2], [3], palette: [9, 9, 9, 255, 0, 0, 0, 255, 0, 0, 0, 255], transparency: [0]), [White, Red, Green, Blue] },
        { "grey with alpha", Png(4, 8, [0, 255], [0, 0], [255, 255], [0, 255]), [Black, White, White, Black] },
        { "colour with alpha", Png(6, 8, [255, 0, 0, 255], [0, 255, 0, 0], [0, 0, 255, 255], [0, 0, 0, 255]), [Red, White, Blue, Black] },
        { "colour with one see-through colour", Png(2, 8, [255, 0, 0], [0, 255, 0], [0, 0, 255], [255, 0, 0], transparency: [0, 255, 0, 0, 0, 0]), [White, Green, Blue, White] },
    };

    [PdfLinesTheory]
    [MemberData(nameof(Pngs))]
    public async Task Every_kind_of_PNG_is_drawn_with_its_colours_and_transparency(string kind, byte[] png, (int, int, int)[] expected)
    {
        var page = PdfRender.FirstPage(await Pdf(Docx(png, "image/png", Picture(72, 72, "Four squares"))));

        var corners = new[] { (18, 18), (54, 18), (18, 54), (54, 54) }.Select(c => page.At(Margin + c.Item1, Margin + c.Item2)).ToArray();
        corners.Should().Equal(expected, kind);
    }

    [Fact]
    public async Task A_JPEG_goes_into_the_PDF_as_it_is()
    {
        var jpeg = await File.ReadAllBytesAsync(Path.Combine(AppContext.BaseDirectory, "Fixtures", "Pictures", "checks.jpg"));
        var pdf = await Pdf(Docx(jpeg, "image/jpeg", Picture(40, 40, "Checks")));

        pdf.AsSpan().IndexOf(jpeg).Should().BeGreaterThan(0);
        Encoding.Latin1.GetString(pdf).Should().Contain("/Filter /DCTDecode");
    }

    [ComparedPdfTheory]
    [InlineData(30)]
    [InlineData(120)]
    public async Task A_picture_in_a_line_is_sized_and_placed_as_LibreOffice_places_it(int height)
    {
        var docx = Rendered(Docx(Png(2, 8, [255, 0, 0], [0, 255, 0], [0, 0, 255], [0, 0, 0]), "image/png",
            $"<w:p {W}><w:r><w:t>Before</w:t></w:r></w:p>",
            $"<w:p {W}><w:r><w:t xml:space=\"preserve\">Text and </w:t></w:r>{Run(60, height, "Squares")}<w:r><w:t xml:space=\"preserve\"> more text</w:t></w:r></w:p>",
            $"<w:p {W}><w:r><w:t>After</w:t></w:r></w:p>"));
        var theirs = await new LibreOfficePdfConverter().ConvertAsync(docx);
        var ours = await new HeliographPdfConverter().ConvertAsync(docx);
        var expected = PdfLines.Read(theirs);
        var actual = PdfLines.Read(ours);

        actual.Select(l => l.Text).Should().Equal(expected.Select(l => l.Text));
        actual.Zip(expected).Should().OnlyContain(pair => Math.Abs(pair.First.Top - pair.Second.Top) <= 1.5);
    }

    [VeraPdfTheory]
    [InlineData("Our logo", false)]
    [InlineData("", true)]
    [InlineData("", false)]
    public async Task A_picture_described_decorative_or_undescribed_is_still_an_accessible_PDF(string description, bool decorative)
    {
        var pdf = await Pdf(Docx(Png(2, 8, [255, 0, 0], [0, 255, 0], [0, 0, 255], [0, 0, 0]), "image/png", Picture(40, 40, description, decorative)),
            allowWarnings: true);

        PdfValidator.FailuresAsPdfUa(pdf).Should().BeEmpty();
    }

    [Fact]
    public async Task A_described_picture_is_a_figure_with_its_description_and_any_other_is_passed_over()
    {
        var png = Png(2, 8, [255, 0, 0], [0, 255, 0], [0, 0, 255], [0, 0, 0]);
        var described = Encoding.Latin1.GetString(await Pdf(Docx(png, "image/png", Picture(40, 40, "Our logo"))));
        var decorative = Encoding.Latin1.GetString(await Pdf(Docx(png, "image/png", Picture(40, 40, "", decorative: true))));

        described.Should().Contain("/S /Figure").And.Contain("/Alt <FEFF" + Convert.ToHexString(Encoding.BigEndianUnicode.GetBytes("Our logo")) + ">");
        decorative.Should().NotContain("/S /Figure");
    }

    public static TheoryData<string, byte[], string> CannotDraw => new()
    {
        { "a GIF", "GIF89a\u0001\u0000\u0001\u0000"u8.ToArray(), "not a PNG or JPEG" },
        { "a damaged PNG", Png(2, 8, [255, 0, 0], [0, 255, 0], [0, 0, 255], [0, 0, 0])[..60], "damaged" },
        { "an enormous PNG", Enormous(), "too large" },
    };

    [Theory]
    [MemberData(nameof(CannotDraw))]
    public void A_picture_file_the_converter_cannot_draw_is_refused_at_compile(string kind, byte[] file, string named)
    {
        var compilation = DocumentTemplate.Compile(Docx(file, "image/png", Picture(40, 40, "A picture")), NoFields, [new HeliographPdfConverter()]);

        compilation.Diagnostics.Should().ContainSingle(d => d.Code == DiagnosticCodes.PdfCannotDrawPicture, kind)
            .Which.Message.Should().Contain(named);
        compilation.Succeeded.Should().BeFalse();
    }

    [Fact]
    public void A_picture_with_text_wrapped_tight_to_it_is_refused_at_compile()
    {
        var floating = Picture(40, 40, "A picture").Replace("wp:inline", "wp:anchor", StringComparison.Ordinal)
            .Replace("<wp:extent", "<wp:wrapTight wrapText=\"bothSides\"/><wp:extent", StringComparison.Ordinal);
        var compilation = DocumentTemplate.Compile(Docx(Png(2, 8, [0, 0, 0], [0, 0, 0], [0, 0, 0], [0, 0, 0]), "image/png", floating), NoFields, [new HeliographPdfConverter()]);

        compilation.Diagnostics.Should().ContainSingle(d => d.Code == DiagnosticCodes.PdfCannotDrawPicture).Which.Message.Should().Contain("tight to its outline");
    }

    [Theory]
    [InlineData("", false, true)]
    [InlineData("  ", false, true)]
    [InlineData("", true, false)]
    [InlineData("Our logo", false, false)]
    public void A_picture_with_no_description_is_a_warning_on_every_route(string description, bool decorative, bool warned)
    {
        var compilation = DocumentTemplate.Compile(Docx(Png(2, 8, [0, 0, 0], [0, 0, 0], [0, 0, 0], [0, 0, 0]), "image/png", Picture(40, 40, description, decorative)), NoFields);

        compilation.Succeeded.Should().BeTrue();
        compilation.Diagnostics.Any(d => d.Code == DiagnosticCodes.PictureHasNoDescription && d.Severity == DiagnosticSeverity.Warning).Should().Be(warned);
    }

    // A two-by-two PNG from its four pixels' samples.
    private static byte[] Png(int colorType, int bitDepth, int[] topLeft, int[] topRight, int[] bottomLeft, int[] bottomRight,
        bool interlaced = false, byte[]? palette = null, byte[]? transparency = null)
    {
        int[][] pixels = [topLeft, topRight, bottomLeft, bottomRight];
        return TestPng.Write(2, 2, colorType, bitDepth, (x, y) => pixels[(y * 2) + x], interlaced, palette, transparency);
    }

    // A PNG whose header claims 10,000 by 10,000 pixels.
    private static byte[] Enormous()
    {
        var png = TestPng.Write(1, 1, 0, 8, (_, _) => [0]);
        System.Buffers.Binary.BinaryPrimitives.WriteInt32BigEndian(png.AsSpan(16), 10_000);
        System.Buffers.Binary.BinaryPrimitives.WriteInt32BigEndian(png.AsSpan(20), 10_000);
        return png;
    }

    private static string Picture(int width, int height, string description, bool decorative = false) =>
        $"<w:p {W}>{Run(width, height, description, decorative)}</w:p>";

    private static string Run(int width, int height, string description, bool decorative = false)
    {
        var mark = decorative
            ? "<a:extLst><a:ext uri=\"{C183D7F6-B498-43B3-948B-1728B52AA6E4}\"><adec:decorative xmlns:adec=\"http://schemas.microsoft.com/office/drawing/2017/decorative\" val=\"1\"/></a:ext></a:extLst>"
            : "";
        return $"<w:r {W}><w:drawing><wp:inline distT=\"0\" distB=\"0\" distL=\"0\" distR=\"0\" {Drawing}><wp:extent cx=\"{width * 12700}\" cy=\"{height * 12700}\"/>"
            + $"<wp:docPr id=\"1\" name=\"Picture 1\" descr=\"{description}\">{mark}</wp:docPr>"
            + "<a:graphic><a:graphicData uri=\"http://schemas.openxmlformats.org/drawingml/2006/picture\"><pic:pic>"
            + "<pic:nvPicPr><pic:cNvPr id=\"1\" name=\"Picture 1\"/><pic:cNvPicPr/></pic:nvPicPr>"
            + "<pic:blipFill><a:blip r:embed=\"rIdX0\"/><a:stretch><a:fillRect/></a:stretch></pic:blipFill>"
            + $"<pic:spPr><a:xfrm><a:off x=\"0\" y=\"0\"/><a:ext cx=\"{width * 12700}\" cy=\"{height * 12700}\"/></a:xfrm><a:prstGeom prst=\"rect\"><a:avLst/></a:prstGeom></pic:spPr>"
            + "</pic:pic></a:graphicData></a:graphic></wp:inline></w:drawing></w:r>";
    }

    private static MemoryStream Docx(byte[] picture, string contentType, params string[] paragraphs)
    {
        var docx = new TestDocx().Media("word/media/picture1.png", picture, contentType).Relationship(ImageRelationship, "media/picture1.png", external: false)
            .Part("word/styles.xml", Styles, "application/vnd.openxmlformats-officedocument.wordprocessingml.styles+xml")
            .Relationship("http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles", "styles.xml", external: false);
        foreach (var p in paragraphs)
        {
            docx.RawParagraph(p);
        }

        return docx.Build();
    }

    private static byte[] Rendered(MemoryStream template, bool allowWarnings = false)
    {
        var compilation = DocumentTemplate.Compile(template, NoFields, [new HeliographPdfConverter()]);
        if (!allowWarnings)
        {
            compilation.Diagnostics.Should().BeEmpty();
        }

        return compilation.Document!.Render(JsonDocument.Parse("{}").RootElement, Benchmark.British).Docx!;
    }

    private static Task<byte[]> Pdf(MemoryStream template, bool allowWarnings = false) => new HeliographPdfConverter().ConvertAsync(Rendered(template, allowWarnings));
}
