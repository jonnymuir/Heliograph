using System.Text.Json;
using Heliograph.Documents;
using Heliograph.Pdf;
using Heliograph.Pdf.LibreOffice;
using Heliograph.Tests.Support;

namespace Heliograph.Tests;

/// <summary>
/// Floating pictures with text wrapped round them in Heliograph's own PDF converter: beside a picture at either margin,
/// below one set above and below the text, and on the wider side of one in the middle, laid out as LibreOffice lays them
/// out, each line where it starts across the page as well as down it.
/// </summary>
public class PdfWrappedPictureTests
{
    private const string W = "xmlns:w=\"http://schemas.openxmlformats.org/wordprocessingml/2006/main\"";
    private const string Drawing = "xmlns:wp=\"http://schemas.openxmlformats.org/drawingml/2006/wordprocessingDrawing\" "
        + "xmlns:a=\"http://schemas.openxmlformats.org/drawingml/2006/main\" xmlns:pic=\"http://schemas.openxmlformats.org/drawingml/2006/picture\" "
        + "xmlns:r=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships\"";
    private const string Words = "Thank you for your application to the National Juggling Federation, which we received on 3 March and will answer soon.";
    private static readonly FieldDictionary NoFields = new([]);
    private static readonly byte[] Blue = TestPng.Write(2, 2, 2, 8, (_, _) => [0, 0, 255]);

    public static TheoryData<string, string, string> Wraps => new()
    {
        { "beside a picture at the left margin", "left", "<wp:wrapSquare wrapText=\"bothSides\"/>" },
        { "beside a picture at the right margin", "right", "<wp:wrapSquare wrapText=\"bothSides\"/>" },
        { "above and below a picture", "left", "<wp:wrapTopAndBottom/>" },
        { "on the wider side of a picture off centre", "offset", "<wp:wrapSquare wrapText=\"largest\"/>" },
    };

    [ComparedPdfTheory]
    [MemberData(nameof(Wraps))]
    public async Task Text_wraps_round_a_picture_as_LibreOffice_wraps_it(string kind, string place, string wrap)
    {
        var docx = Rendered(Docx(Anchor(place, wrap)));
        var expected = PdfLines.Read(await new LibreOfficePdfConverter().ConvertAsync(docx));
        var actual = PdfLines.Read(await new HeliographPdfConverter().ConvertAsync(docx));

        actual.Select(l => l.Text).Should().Equal(expected.Select(l => l.Text), kind);
        actual.Zip(expected).Should().OnlyContain(pair => Math.Abs(pair.First.Top - pair.Second.Top) <= 1.5 && Math.Abs(pair.First.Left - pair.Second.Left) <= 1.5, kind);
    }

    [ComparedPdfFact]
    public async Task A_table_starts_below_a_picture_it_would_meet_as_LibreOffice_lays_it_out()
    {
        var table = $"<w:tbl {W}><w:tblPr><w:tblW w:w=\"5000\" w:type=\"dxa\"/><w:tblBorders><w:top w:val=\"single\" w:sz=\"4\"/><w:bottom w:val=\"single\" w:sz=\"4\"/></w:tblBorders></w:tblPr><w:tblGrid><w:gridCol w:w=\"5000\"/></w:tblGrid><w:tr><w:tc><w:p><w:r><w:t>A cell</w:t></w:r></w:p></w:tc></w:tr><w:tr><w:tc><w:p><w:r><w:t>Another cell</w:t></w:r></w:p></w:tc></w:tr></w:tbl>";
        var docx = Styled().RawParagraph($"<w:p {W}>{Anchor("left", "<wp:wrapSquare wrapText=\"bothSides\"/>")}<w:r><w:t>Short.</w:t></w:r></w:p>")
            .RawParagraph(table).RawParagraph($"<w:p {W}><w:r><w:t>After.</w:t></w:r></w:p>").Build();
        var rendered = Rendered(docx);
        var expected = PdfLines.Read(await new LibreOfficePdfConverter().ConvertAsync(rendered));
        var actual = PdfLines.Read(await new HeliographPdfConverter().ConvertAsync(rendered));

        PdfComparison.FirstDifference(expected, actual, "LibreOffice").Difference.Should().BeNull();
    }

    [Theory]
    [InlineData("left", "<wp:wrapTight wrapText=\"bothSides\"><wp:wrapPolygon edited=\"0\"><wp:start x=\"0\" y=\"0\"/><wp:lineTo x=\"0\" y=\"21600\"/><wp:lineTo x=\"21600\" y=\"21600\"/><wp:lineTo x=\"21600\" y=\"0\"/><wp:lineTo x=\"0\" y=\"0\"/></wp:wrapPolygon></wp:wrapTight>", "tight to its outline")]
    [InlineData("center", "<wp:wrapSquare wrapText=\"bothSides\"/>", "both sides")]
    public void A_wrap_it_cannot_lay_out_is_refused_at_compile(string place, string wrap, string named)
    {
        DocumentTemplate.Compile(Docx(Anchor(place, wrap)), NoFields, [new HeliographPdfConverter()]).Diagnostics
            .Should().ContainSingle(d => d.Code == DiagnosticCodes.PdfCannotDrawPicture).Which.Message.Should().Contain(named);
    }

    [VeraPdfTheory]
    [InlineData("left")]
    public async Task A_letter_with_a_wrapped_picture_is_still_an_accessible_PDF(string place) =>
        PdfValidator.FailuresAsPdfUa(await new HeliographPdfConverter().ConvertAsync(Rendered(Docx(Anchor(place, "<wp:wrapSquare wrapText=\"bothSides\"/>"))))).Should().BeEmpty();

    // A blue picture an inch and a half wide and an inch high at the top of its paragraph: at the left or right margin,
    // in the middle of it, or an inch in from its left.
    private static string Anchor(string place, string wrap)
    {
        var across = place == "offset"
            ? "<wp:positionH relativeFrom=\"margin\"><wp:posOffset>914400</wp:posOffset></wp:positionH>"
            : $"<wp:positionH relativeFrom=\"margin\"><wp:align>{place}</wp:align></wp:positionH>";
        return $"<w:r {W}><w:drawing><wp:anchor distT=\"0\" distB=\"0\" distL=\"114300\" distR=\"114300\" simplePos=\"0\" relativeHeight=\"2\" behindDoc=\"0\" locked=\"0\" layoutInCell=\"1\" allowOverlap=\"1\" {Drawing}>"
            + $"<wp:simplePos x=\"0\" y=\"0\"/>{across}<wp:positionV relativeFrom=\"paragraph\"><wp:posOffset>0</wp:posOffset></wp:positionV>"
            + $"<wp:extent cx=\"1371600\" cy=\"914400\"/><wp:effectExtent l=\"0\" t=\"0\" r=\"0\" b=\"0\"/>{wrap}"
            + "<wp:docPr id=\"1\" name=\"Picture 1\" descr=\"A blue square\"/><a:graphic><a:graphicData uri=\"http://schemas.openxmlformats.org/drawingml/2006/picture\"><pic:pic>"
            + "<pic:nvPicPr><pic:cNvPr id=\"1\" name=\"Picture 1\"/><pic:cNvPicPr/></pic:nvPicPr><pic:blipFill><a:blip r:embed=\"rIdX0\"/><a:stretch><a:fillRect/></a:stretch></pic:blipFill>"
            + "<pic:spPr><a:xfrm><a:off x=\"0\" y=\"0\"/><a:ext cx=\"1371600\" cy=\"914400\"/></a:xfrm><a:prstGeom prst=\"rect\"><a:avLst/></a:prstGeom></pic:spPr></pic:pic></a:graphicData></a:graphic>"
            + "</wp:anchor></w:drawing></w:r>";
    }

    // A paragraph, then one with the picture anchored at its start and enough text to run past it, then two more.
    private static MemoryStream Docx(string anchor) =>
        Styled().RawParagraph($"<w:p {W}><w:r><w:t>Dear Ms Example, this is the first paragraph.</w:t></w:r></w:p>")
            .RawParagraph($"<w:p {W}>{anchor}<w:r><w:t>{Words} {Words} {Words}</w:t></w:r></w:p>")
            .RawParagraph($"<w:p {W}><w:r><w:t>{Words}</w:t></w:r></w:p>")
            .RawParagraph($"<w:p {W}><w:r><w:t>A last paragraph.</w:t></w:r></w:p>").Build();

    // A letter in Arial with the blue picture, as each converter lays out an unstyled one in a default of its own.
    private static TestDocx Styled()
    {
        var styles = $"<w:styles {W}><w:docDefaults><w:rPrDefault><w:rPr><w:rFonts w:ascii=\"Arial\" w:hAnsi=\"Arial\"/><w:sz w:val=\"22\"/></w:rPr></w:rPrDefault>"
            + "<w:pPrDefault><w:pPr><w:spacing w:after=\"160\" w:line=\"259\" w:lineRule=\"auto\"/></w:pPr></w:pPrDefault></w:docDefaults></w:styles>";
        return new TestDocx().Media("word/media/picture1.png", Blue, "image/png").Relationship("http://schemas.openxmlformats.org/officeDocument/2006/relationships/image", "media/picture1.png", external: false)
            .Part("word/styles.xml", styles, "application/vnd.openxmlformats-officedocument.wordprocessingml.styles+xml")
            .Relationship("http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles", "styles.xml", external: false);
    }

    private static byte[] Rendered(MemoryStream template)
    {
        var compilation = DocumentTemplate.Compile(template, NoFields, [new HeliographPdfConverter()]);
        compilation.Diagnostics.Should().BeEmpty();
        return compilation.Document!.Render(JsonDocument.Parse("{}").RootElement, Benchmark.British).Docx!;
    }
}
