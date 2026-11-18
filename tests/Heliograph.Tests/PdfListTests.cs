using System.Text;
using System.Text.Json;
using Heliograph.Documents;
using Heliograph.Pdf;
using Heliograph.Tests.Support;

namespace Heliograph.Tests;

/// <summary>
/// Lists in Heliograph's own PDF converter, labelled as Word labels them: each level's number style, labels that show
/// higher levels, legal numbering, restarts, lists that count on from another, and Word's bullets.
/// </summary>
public class PdfListTests
{
    private const string W = "xmlns:w=\"http://schemas.openxmlformats.org/wordprocessingml/2006/main\"";
    private const string NumberingType = "application/vnd.openxmlformats-officedocument.wordprocessingml.numbering+xml";
    private static readonly FieldDictionary NoFields = new([]);

    [PdfLinesFact]
    public async Task Each_level_is_numbered_in_its_own_style()
    {
        var levels = Level(0, "decimalZero", "%1.") + Level(1, "lowerLetter", "%2)") + Level(2, "upperRoman", "%3.");
        var labels = await Labels(Lists(levels), Items(1, (0, 1), (1, 3), (2, 4)));

        labels.Should().Equal("01.", "a)", "b)", "c)", "I.", "II.", "III.", "IV.");
    }

    [PdfLinesFact]
    public async Task Letters_run_on_past_z_as_Word_runs_them()
    {
        var labels = await Labels(Lists(Level(0, "upperLetter", "%1.", start: 26)), Items(1, (0, 3)));

        labels.Should().Equal("Z.", "AA.", "BB.");
    }

    [PdfLinesFact]
    public async Task A_label_shows_its_higher_levels_and_a_deeper_level_starts_again_after_a_higher_one()
    {
        var levels = Level(0, "decimal", "%1.") + Level(1, "decimal", "%1.%2.") + Level(2, "lowerRoman", "%1.%2.%3");
        var labels = await Labels(Lists(levels), Items(1, (0, 1), (1, 2), (2, 1), (0, 1), (1, 1)));

        labels.Should().Equal("1.", "1.1.", "1.2.", "1.2.i", "2.", "2.1.");
    }

    [PdfLinesFact]
    public async Task A_legal_label_shows_every_level_as_a_plain_number()
    {
        var levels = Level(0, "upperRoman", "%1.") + Level(1, "decimal", "%1.%2.", legal: true);
        var labels = await Labels(Lists(levels), Items(1, (0, 2), (1, 1)));

        labels.Should().Equal("I.", "II.", "2.1.");
    }

    [PdfLinesFact]
    public async Task Lists_sharing_a_definition_count_on_unless_one_starts_again()
    {
        var numbering = Lists(Level(0, "decimal", "%1."), extra: "<w:num w:numId=\"2\"><w:abstractNumId w:val=\"0\"/></w:num>"
            + "<w:num w:numId=\"3\"><w:abstractNumId w:val=\"0\"/><w:lvlOverride w:ilvl=\"0\"><w:startOverride w:val=\"1\"/></w:lvlOverride></w:num>");
        var labels = await Labels(numbering, [.. Items(1, (0, 2)), .. Items(2, (0, 1)), .. Items(3, (0, 2))]);

        labels.Should().Equal("1.", "2.", "3.", "1.", "2.");
    }

    [PdfLinesFact]
    public async Task Words_bullets_in_Symbol_Courier_New_and_Wingdings_are_drawn_as_the_same_shapes()
    {
        var levels = Level(0, "bullet", "", font: "Symbol") + Level(1, "bullet", "o", font: "Courier New") + Level(2, "bullet", "", font: "Wingdings");
        var labels = await Labels(Lists(levels), Items(1, (0, 1), (1, 1), (2, 1)));

        labels.Should().Equal("•", "o", "▪");
    }

    [VeraPdfTheory]
    [InlineData(3)]
    public async Task A_list_inside_a_list_is_still_an_accessible_PDF(int depth)
    {
        var levels = string.Concat(Enumerable.Range(0, depth).Select(l => Level(l, "decimal", $"%{l + 1}.")));
        var items = Items(1, [.. Enumerable.Range(0, depth).Select(l => (l, 2)), (0, 1)]);

        PdfValidator.FailuresAsPdfUa(await Pdf(Lists(levels), items)).Should().BeEmpty();
    }

    public static TheoryData<string, string> CannotDraw => new()
    {
        { Level(0, "ordinal", "%1"), "\"ordinal\"" },
        { Level(0, "bullet", "", font: "Wingdings"), "bullet character" },
        { Level(0, "bullet", "•", picture: true), "picture" },
    };

    [Theory]
    [MemberData(nameof(CannotDraw))]
    public void A_list_label_the_converter_cannot_draw_is_refused_at_compile(string level, string named)
    {
        var compilation = DocumentTemplate.Compile(Docx(Lists(level), Items(1, (0, 2))), NoFields, [new HeliographPdfConverter()]);

        var diagnostic = compilation.Diagnostics.Should().ContainSingle(d => d.Code == DiagnosticCodes.PdfCannotDrawListLabel).Subject;
        diagnostic.Message.Should().Contain(named);
        diagnostic.Location!.Paragraph.Should().Be(1);
        compilation.Succeeded.Should().BeFalse();
    }

    // Paragraphs in list numId: for each (level, count), that many items at that level.
    private static List<string> Items(int numId, params (int Level, int Count)[] runs) =>
        [.. runs.SelectMany(r => Enumerable.Repeat(r.Level, r.Count)).Select((level, i) =>
            $"<w:p {W}><w:pPr><w:numPr><w:ilvl w:val=\"{level}\"/><w:numId w:val=\"{numId}\"/></w:numPr></w:pPr><w:r><w:t>Item {numId}-{i + 1}</w:t></w:r></w:p>")];

    private static string Level(int level, string format, string text, int start = 1, bool legal = false, string? font = null, bool picture = false) =>
        $"<w:lvl w:ilvl=\"{level}\"><w:start w:val=\"{start}\"/><w:numFmt w:val=\"{format}\"/>{(legal ? "<w:isLgl/>" : "")}"
        + $"{(picture ? "<w:lvlPicBulletId w:val=\"0\"/>" : "")}<w:lvlText w:val=\"{text}\"/><w:lvlJc w:val=\"left\"/>"
        + $"<w:pPr><w:ind w:left=\"{1440 + (720 * level)}\" w:hanging=\"720\"/></w:pPr>"
        + (font is null ? "" : $"<w:rPr><w:rFonts w:ascii=\"{font}\" w:hAnsi=\"{font}\"/></w:rPr>") + "</w:lvl>";

    private static string Lists(string levels, string extra = "") =>
        $"<w:numbering {W}><w:abstractNum w:abstractNumId=\"0\">{levels}</w:abstractNum><w:num w:numId=\"1\"><w:abstractNumId w:val=\"0\"/></w:num>{extra}</w:numbering>";

    private static MemoryStream Docx(string numbering, IEnumerable<string> paragraphs)
    {
        var docx = new TestDocx().Part("word/numbering.xml", numbering, NumberingType)
            .Relationship("http://schemas.openxmlformats.org/officeDocument/2006/relationships/numbering", "numbering.xml", external: false);
        foreach (var p in paragraphs)
        {
            docx.RawParagraph(p);
        }

        return docx.Build();
    }

    private static async Task<byte[]> Pdf(string numbering, IEnumerable<string> paragraphs)
    {
        var compilation = DocumentTemplate.Compile(Docx(numbering, paragraphs), NoFields, [new HeliographPdfConverter()]);
        compilation.Diagnostics.Should().BeEmpty();
        var docx = compilation.Document!.Render(JsonDocument.Parse("{}").RootElement, Benchmark.British).Docx!;
        return await new HeliographPdfConverter().ConvertAsync(docx);
    }

    // Each item's label: the text before "Item" on its line.
    private static async Task<List<string>> Labels(string numbering, IEnumerable<string> paragraphs) =>
        [.. PdfLines.Read(await Pdf(numbering, paragraphs)).Select(l => l.Text).Where(t => t.Contains("Item", StringComparison.Ordinal))
            .Select(t => t[..t.IndexOf("Item", StringComparison.Ordinal)].Trim())];
}
