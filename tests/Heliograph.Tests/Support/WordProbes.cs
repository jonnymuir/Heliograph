using System.Text.Json;
using Heliograph.Documents;

namespace Heliograph.Tests.Support;

/// <summary>
/// The documents Word is asked to lay out, as rendered letters: the benchmark letters, a few of the measuring net's
/// documents, and small probes, one for each rule the converter took from LibreOffice where Word may differ (Symbol
/// bullets, exact lines in Calibri, superscript, a tab at the line's end, space between paragraphs of one style, row
/// heights, merged cells).
/// </summary>
internal static class WordProbes
{
    private const string W = "xmlns:w=\"http://schemas.openxmlformats.org/wordprocessingml/2006/main\"";
    private const string Rel = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
    private const string Words = "Thank you for your application to the National Juggling Federation, which we received on 3 March and will answer soon.";

    public static IEnumerable<(string Name, byte[] Docx)> All()
    {
        foreach (var (letter, sample) in new[] { ("acknowledgement", "typical"), ("decision-letter", "approved"), ("annual-statement", "weekly-payer") })
        {
            yield return ($"benchmark-{letter}-{sample}", Benchmark.Compile(letter).Document!.Render(Benchmark.Sample(letter, sample), Benchmark.British).Docx!);
        }

        foreach (var seed in new[] { 1, 2, 3, 4 })
        {
            yield return ($"net-{seed}", Rendered(new MemoryStream(new RandomLayout(seed).Docx())));
        }

        yield return ("net-lists-1", Rendered(new MemoryStream(new RandomLayout(1, LayoutMix.MostlyLists).Docx())));
        foreach (var probe in Probes())
        {
            yield return (probe.Name, Rendered(probe.Template));
        }
    }

    private static IEnumerable<(string Name, MemoryStream Template)> Probes()
    {
        yield return ("probe-bullets", Letter("Arial", lists: true, Body(
            Item(3, 0, "Entry to every meeting"), Item(3, 1, "Quarterly newsletter"), Item(3, 2, "Insurance at events"), Item(5, 0, "A bullet from Symbol"),
            Item(5, 1, "A bullet from Courier New"), Item(5, 2, "A bullet from Wingdings"), Plain(Words))));
        yield return ("probe-calibri-exact", Letter("Calibri", lists: false, Body(
            Spaced(Words + " " + Words, "w:line=\"300\" w:lineRule=\"exact\""), Spaced(Words + " " + Words, "w:line=\"320\" w:lineRule=\"atLeast\""), Plain(Words))));
        yield return ("probe-superscript", Letter("Arial", lists: false, Body(
            Plain("A plain line"), Run("All of this line is superscript", "<w:vertAlign w:val=\"superscript\"/>"),
            Run("All of this line is subscript", "<w:vertAlign w:val=\"subscript\"/>"), Plain("Another plain line"), Spaced(Words, "w:line=\"360\" w:lineRule=\"auto\""))));
        yield return ("probe-tabs", Letter("Arial", lists: false, Body(
            Tabbed("<w:tab w:val=\"right\" w:pos=\"9026\"/>", "Dear Ms Example\tFirst\tSecond"), Tabbed("<w:tab w:val=\"right\" w:leader=\"dot\" w:pos=\"9026\"/>", "Membership\t£45.00"),
            Tabbed("<w:tab w:val=\"decimal\" w:pos=\"7200\"/>", "Total\t£1,048.50"), Tabbed("", "e.g. a word just before a default tab\ttelephone: 0300 123 4567 with more words to wrap the line"))));
        yield return ("probe-contextual", Letter("Arial", lists: false, Body(
            Plain(Words), Styled("ListParagraph", "Entry to every meeting"), Styled("ListParagraph", "The quarterly newsletter"),
            Spaced("A paragraph that says to leave out space, after one that does not.", "w:before=\"180\" w:after=\"80\"", contextual: true),
            Spaced("One that does not, after one that does.", "w:before=\"180\" w:after=\"240\""), Plain(Words))));
        yield return ("probe-tables", Letter("Arial", lists: false, Body(Plain("Your fees for the year."), Table(), Plain("Please pay by the end of March.")), tables: true));
    }

    private static byte[] Rendered(MemoryStream template) =>
        DocumentTemplate.Compile(template, new FieldDictionary([])).Document!.Render(JsonDocument.Parse("{}").RootElement, Benchmark.British).Docx!;

    private static string Body(params string[] paragraphs) => string.Concat(paragraphs);

    private static string Plain(string text) => $"<w:p><w:r><w:t xml:space=\"preserve\">{text}</w:t></w:r></w:p>";

    private static string Run(string text, string rPr) => $"<w:p><w:r><w:rPr>{rPr}</w:rPr><w:t>{text}</w:t></w:r></w:p>";

    private static string Spaced(string text, string spacing, bool contextual = false) =>
        $"<w:p><w:pPr><w:spacing {spacing}/>{(contextual ? "<w:contextualSpacing/>" : "")}</w:pPr><w:r><w:t>{text}</w:t></w:r></w:p>";

    private static string Styled(string style, string text) => $"<w:p><w:pPr><w:pStyle w:val=\"{style}\"/></w:pPr><w:r><w:t>{text}</w:t></w:r></w:p>";

    private static string Item(int numId, int level, string text) =>
        $"<w:p><w:pPr><w:numPr><w:ilvl w:val=\"{level}\"/><w:numId w:val=\"{numId}\"/></w:numPr></w:pPr><w:r><w:t>{text}</w:t></w:r></w:p>";

    private static string Tabbed(string tabs, string line) =>
        $"<w:p><w:pPr><w:tabs>{tabs}</w:tabs></w:pPr>{string.Join("<w:r><w:tab/></w:r>", line.Split('\t').Select(part => $"<w:r><w:t xml:space=\"preserve\">{part}</w:t></w:r>"))}</w:p>";

    // A Table Grid table: rows at least and exactly a set height, and a cell merged down two rows.
    private static string Table()
    {
        static string Cell(string text, string tcPr = "") => $"<w:tc><w:tcPr><w:tcW w:w=\"2700\" w:type=\"dxa\"/>{tcPr}</w:tcPr><w:p><w:r><w:t>{text}</w:t></w:r></w:p></w:tc>";
        var rows = $"<w:tr><w:trPr><w:trHeight w:val=\"600\"/></w:trPr>{Cell("Membership")}{Cell("£45.00")}</w:tr>"
            + $"<w:tr><w:trPr><w:trHeight w:val=\"480\" w:hRule=\"exact\"/></w:trPr>{Cell("Insurance")}{Cell("£12.50")}</w:tr>"
            + $"<w:tr>{Cell("Newsletter by post, printed and posted four times a year", "<w:vMerge w:val=\"restart\"/>")}{Cell("£3.50")}</w:tr>"
            + $"<w:tr>{Cell("", "<w:vMerge/>")}{Cell("£1.00")}</w:tr>";
        return $"<w:tbl><w:tblPr><w:tblStyle w:val=\"TableGrid\"/><w:tblW w:w=\"5400\" w:type=\"dxa\"/></w:tblPr><w:tblGrid><w:gridCol w:w=\"2700\"/><w:gridCol w:w=\"2700\"/></w:tblGrid>{rows}</w:tbl>";
    }

    // A letter in one font, through the theme as Word writes it, with Word's List Paragraph and, when asked, the
    // measuring net's lists and Word's Table Grid.
    private static MemoryStream Letter(string font, bool lists, string body, bool tables = false)
    {
        var styles = $"<w:styles {W}><w:docDefaults><w:rPrDefault><w:rPr><w:rFonts w:asciiTheme=\"minorHAnsi\" w:hAnsiTheme=\"minorHAnsi\"/><w:sz w:val=\"22\"/></w:rPr></w:rPrDefault>"
            + "<w:pPrDefault><w:pPr><w:spacing w:after=\"160\" w:line=\"259\" w:lineRule=\"auto\"/></w:pPr></w:pPrDefault></w:docDefaults>"
            + "<w:style w:type=\"paragraph\" w:default=\"1\" w:styleId=\"Normal\"><w:name w:val=\"Normal\"/></w:style>"
            + "<w:style w:type=\"paragraph\" w:styleId=\"ListParagraph\"><w:name w:val=\"List Paragraph\"/><w:basedOn w:val=\"Normal\"/><w:pPr><w:ind w:left=\"720\"/><w:contextualSpacing/></w:pPr></w:style>"
            + (tables ? RandomTables.Styles : "") + "</w:styles>";
        var docx = new TestDocx()
            .Part("word/styles.xml", styles, "application/vnd.openxmlformats-officedocument.wordprocessingml.styles+xml").Relationship($"{Rel}/styles", "styles.xml", external: false)
            .Part("word/theme/theme1.xml", TestTheme.Xml(font, font), "application/vnd.openxmlformats-officedocument.theme+xml").Relationship($"{Rel}/theme", "theme/theme1.xml", external: false);
        if (lists)
        {
            docx.Part("word/numbering.xml", $"<w:numbering {W}>{RandomLists.Definitions}{RandomLists.Lists}</w:numbering>", "application/vnd.openxmlformats-officedocument.wordprocessingml.numbering+xml")
                .Relationship($"{Rel}/numbering", "numbering.xml", external: false);
        }

        docx.RawParagraph(body);
        return docx.Build();
    }
}
