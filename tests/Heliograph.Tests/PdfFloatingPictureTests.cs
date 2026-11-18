using System.Text.Json;
using Heliograph.Documents;
using Heliograph.Pdf;
using Heliograph.Pdf.LibreOffice;
using Heliograph.Tests.Support;

namespace Heliograph.Tests;

/// <summary>
/// Pictures that float in Heliograph's own PDF converter, with no text wrapped round them: a logo in a header, a picture in
/// front of or behind the text, placed where LibreOffice places them.
/// </summary>
public class PdfFloatingPictureTests
{
    private const string W = "xmlns:w=\"http://schemas.openxmlformats.org/wordprocessingml/2006/main\"";
    private const string Drawing = "xmlns:wp=\"http://schemas.openxmlformats.org/drawingml/2006/wordprocessingDrawing\" "
        + "xmlns:a=\"http://schemas.openxmlformats.org/drawingml/2006/main\" xmlns:pic=\"http://schemas.openxmlformats.org/drawingml/2006/picture\" "
        + "xmlns:r=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships\"";
    private static readonly FieldDictionary NoFields = new([]);
    private static readonly byte[] Blue = TestPng.Write(2, 2, 2, 8, (_, _) => [0, 0, 255]);

    public static TheoryData<string, string, string, bool> Anchors => new()
    {
        { "to the right of the page, from its top", "<wp:positionH relativeFrom=\"page\"><wp:align>right</wp:align></wp:positionH>", "<wp:positionV relativeFrom=\"page\"><wp:posOffset>457200</wp:posOffset></wp:positionV>", false },
        { "centred in the margins, from the paragraph", "<wp:positionH relativeFrom=\"margin\"><wp:align>center</wp:align></wp:positionH>", "<wp:positionV relativeFrom=\"paragraph\"><wp:posOffset>127000</wp:posOffset></wp:positionV>", false },
        { "behind the text, offset in the column", "<wp:positionH relativeFrom=\"column\"><wp:posOffset>1270000</wp:posOffset></wp:positionH>", "<wp:positionV relativeFrom=\"paragraph\"><wp:posOffset>0</wp:posOffset></wp:positionV>", true },
    };

    [ComparedPdfTheory]
    [MemberData(nameof(Anchors))]
    public async Task A_floating_picture_is_placed_where_LibreOffice_places_it(string kind, string across, string down, bool behind)
    {
        var docx = Rendered(Docx(Anchor(across, down, behind)));
        var theirs = Box(PdfRender.FirstPage(await new LibreOfficePdfConverter().ConvertAsync(docx)));
        var ours = Box(PdfRender.FirstPage(await new HeliographPdfConverter().ConvertAsync(docx)));

        ours.Should().NotBeNull(kind);
        new[] { ours!.Value.Left - theirs!.Value.Left, ours.Value.Top - theirs.Value.Top }.Should().OnlyContain(d => Math.Abs(d) <= 2, kind);
    }

    [ComparedPdfFact]
    public async Task A_floating_picture_takes_no_room_in_the_lines()
    {
        var docx = Rendered(Docx(Anchor("<wp:positionH relativeFrom=\"column\"><wp:posOffset>0</wp:posOffset></wp:positionH>", "<wp:positionV relativeFrom=\"paragraph\"><wp:posOffset>0</wp:posOffset></wp:positionV>", false)));
        var expected = PdfLines.Read(await new LibreOfficePdfConverter().ConvertAsync(docx));
        var actual = PdfLines.Read(await new HeliographPdfConverter().ConvertAsync(docx));

        actual.Select(l => l.Text).Should().Equal(expected.Select(l => l.Text));
        actual.Zip(expected).Should().OnlyContain(pair => Math.Abs(pair.First.Top - pair.Second.Top) <= 1.5);
    }

    [VeraPdfTheory]
    [InlineData("Our logo")]
    [InlineData("")]
    public async Task A_floating_picture_is_still_an_accessible_PDF(string description) =>
        PdfValidator.FailuresAsPdfUa(await new HeliographPdfConverter().ConvertAsync(Rendered(Docx(Anchor(
            "<wp:positionH relativeFrom=\"margin\"><wp:align>right</wp:align></wp:positionH>", "<wp:positionV relativeFrom=\"paragraph\"><wp:posOffset>0</wp:posOffset></wp:positionV>", false, description)), allowWarnings: true))).Should().BeEmpty();

    // Where the blue picture is drawn: its leftmost column and top row, in points.
    private static (int Left, int Top)? Box(PdfRender page)
    {
        for (var y = 0; y < page.Height; y++)
        {
            for (var x = 0; x < page.Width; x++)
            {
                if (page.At(x, y) is (< 40, < 40, > 200))
                {
                    var left = x;
                    while (left > 0 && page.At(left - 1, y) is (< 40, < 40, > 200))
                    {
                        left--;
                    }

                    return (left, y);
                }
            }
        }

        return null;
    }

    private static string Anchor(string across, string down, bool behind, string description = "A blue square") =>
        $"<w:r {W}><w:drawing><wp:anchor distT=\"0\" distB=\"0\" distL=\"0\" distR=\"0\" simplePos=\"0\" relativeHeight=\"2\" behindDoc=\"{(behind ? 1 : 0)}\" locked=\"0\" layoutInCell=\"1\" allowOverlap=\"1\" {Drawing}>"
        + $"<wp:simplePos x=\"0\" y=\"0\"/>{across}{down}<wp:extent cx=\"914400\" cy=\"457200\"/><wp:effectExtent l=\"0\" t=\"0\" r=\"0\" b=\"0\"/><wp:wrapNone/>"
        + $"<wp:docPr id=\"1\" name=\"Picture 1\" descr=\"{description}\"/><a:graphic><a:graphicData uri=\"http://schemas.openxmlformats.org/drawingml/2006/picture\"><pic:pic>"
        + "<pic:nvPicPr><pic:cNvPr id=\"1\" name=\"Picture 1\"/><pic:cNvPicPr/></pic:nvPicPr><pic:blipFill><a:blip r:embed=\"rIdX0\"/><a:stretch><a:fillRect/></a:stretch></pic:blipFill>"
        + "<pic:spPr><a:xfrm><a:off x=\"0\" y=\"0\"/><a:ext cx=\"914400\" cy=\"457200\"/></a:xfrm><a:prstGeom prst=\"rect\"><a:avLst/></a:prstGeom></pic:spPr></pic:pic></a:graphicData></a:graphic>"
        + "</wp:anchor></w:drawing></w:r>";

    private static MemoryStream Docx(string anchor)
    {
        var styles = $"<w:styles {W}><w:docDefaults><w:rPrDefault><w:rPr><w:rFonts w:ascii=\"Arial\" w:hAnsi=\"Arial\"/><w:sz w:val=\"22\"/></w:rPr></w:rPrDefault></w:docDefaults></w:styles>";
        return new TestDocx().Media("word/media/picture1.png", Blue, "image/png").Relationship("http://schemas.openxmlformats.org/officeDocument/2006/relationships/image", "media/picture1.png", external: false)
            .Part("word/styles.xml", styles, "application/vnd.openxmlformats-officedocument.wordprocessingml.styles+xml")
            .Relationship("http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles", "styles.xml", external: false)
            .RawParagraph($"<w:p {W}><w:r><w:t>Dear Ms Example, this is the first paragraph.</w:t></w:r></w:p>")
            .RawParagraph($"<w:p {W}><w:r><w:t xml:space=\"preserve\">The picture is anchored here </w:t></w:r>{anchor}<w:r><w:t>and the text carries on as if it were not there.</w:t></w:r></w:p>")
            .RawParagraph($"<w:p {W}><w:r><w:t>A last paragraph.</w:t></w:r></w:p>").Build();
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
}
