using System.Text.Json;
using Heliograph.Documents;

namespace Heliograph.Tests.Support;

/// <summary>
/// The documents Word is asked to lay out, as rendered letters: the benchmark letters, a few of the measuring net's
/// documents, and small probes, one for each rule the converter took from LibreOffice where Word may differ (Symbol
/// bullets, exact lines in Calibri, superscript, a tab at the line's end, space between paragraphs of one style, row
/// heights, merged cells, where each font's line gap goes).
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

        // A table row whose footnote does not fit at the foot of the page, which LibreOffice moves leaving a gap above it.
        var body = new List<string> { NoteLetters.Referring("Your membership renews in April.", 1) };
        body.AddRange(Enumerable.Range(0, 27).Select(i => NoteLetters.Plain($"Paragraph {i} of the body of the letter, about the renewal of your membership.")));
        body.Add(NoteTables.FeesTable(2));
        body.Add(NoteLetters.Referring("The late charge is ten pounds.", 3));
        yield return ("probe-page-fields", Rendered(FieldLetters.Letter()));
        yield return ("probe-table-footnote", Rendered(NoteLetters.Letter(body, "Membership runs from April to March.", "Charged on fees paid after the end of April.", "A third note.")));
    }

    private static IEnumerable<(string Name, MemoryStream Template)> Probes()
    {
        yield return ("probe-bullets", Letter("Arial", Extras.Lists, Body(
            Item(3, 0, "Entry to every meeting"), Item(3, 1, "Quarterly newsletter"), Item(3, 2, "Insurance at events"), Item(5, 0, "A bullet from Symbol"),
            Item(5, 1, "A bullet from Courier New"), Item(5, 2, "A bullet from Wingdings"), Plain(Words))));
        yield return ("probe-calibri-exact", Letter("Calibri", Extras.None, Body(
            Spaced(Words + " " + Words, "w:line=\"300\" w:lineRule=\"exact\""), Spaced(Words + " " + Words, "w:line=\"320\" w:lineRule=\"atLeast\""), Plain(Words))));
        yield return ("probe-small-caps", Letter("Arial", Extras.None, Body(Run(Words + " " + Words, "<w:smallCaps/>"), Run("Smith and Partners, Solicitors", "<w:smallCaps/><w:sz w:val=\"32\"/>"), Plain(Words))));
        yield return ("probe-superscript", Letter("Arial", Extras.None, Body(
            Plain("A plain line"), Run("All of this line is superscript", "<w:vertAlign w:val=\"superscript\"/>"),
            Run("All of this line is subscript", "<w:vertAlign w:val=\"subscript\"/>"), Plain("Another plain line"), Spaced(Words, "w:line=\"360\" w:lineRule=\"auto\""))));
        yield return ("probe-tabs", Letter("Arial", Extras.None, Body(
            Tabbed("<w:tab w:val=\"right\" w:pos=\"9026\"/>", "Dear Ms Example\tFirst\tSecond"), Tabbed("<w:tab w:val=\"right\" w:leader=\"dot\" w:pos=\"9026\"/>", "Membership\t£45.00"),
            Tabbed("<w:tab w:val=\"decimal\" w:pos=\"7200\"/>", "Total\t£1,048.50"), Tabbed("", "e.g. a word just before a default tab\ttelephone: 0300 123 4567 with more words to wrap the line"))));
        yield return ("probe-contextual", Letter("Arial", Extras.None, Body(
            Plain(Words), Styled("ListParagraph", "Entry to every meeting"), Styled("ListParagraph", "The quarterly newsletter"),
            Spaced("A paragraph that says to leave out space, after one that does not.", "w:before=\"180\" w:after=\"80\"", contextual: true),
            Spaced("One that does not, after one that does.", "w:before=\"180\" w:after=\"240\""), Plain(Words))));
        // Each pair of paragraphs a case of the rule: one that says to leave out space, then one that does not, and so on.
        var pairs = Body(Plain(Words), Spaced("Leaves out space, with 9 after.", "w:before=\"0\" w:after=\"180\"", contextual: true),
            Spaced("Does not, with 9 before and 6 after.", "w:before=\"180\" w:after=\"120\""), Spaced("Leaves out space, with 4 before.", "w:before=\"80\" w:after=\"0\"", contextual: true),
            Spaced("Leaves out space, with 10 before and 5 after.", "w:before=\"200\" w:after=\"100\"", contextual: true), Spaced("Does not, with 3 before.", "w:before=\"60\" w:after=\"0\""),
            Plain(Words));
        yield return ("probe-contextual-pairs", Letter("Arial", Extras.None, pairs));
        yield return ("probe-contextual-sum", Letter("Arial", Extras.Sum, pairs));
        // Each font set in an exact line, where Word and the converter agree, then in a single line, to show where its gap goes.
        yield return ("probe-gaps", Letter("Arial", Extras.None, Body(new[] { ("Calibri", 22), ("Calibri", 40), ("Arial", 22), ("Times New Roman", 22), ("Cambria", 22) }
            .SelectMany(f => new[] { InFont(f.Item1, f.Item2, "w:line=\"480\" w:lineRule=\"exact\""), InFont(f.Item1, f.Item2, "w:line=\"240\" w:lineRule=\"auto\"") }).ToArray())));
        yield return ("probe-nested-table", PdfNestedTableTests.Letter(around: true));
        yield return ("probe-nested-table-alone", PdfNestedTableTests.Letter(around: false));
        yield return ("probe-page-breaks", Letter("Arial", Extras.None, Body(Plain(Words),
            "<w:p><w:r><w:t>Before the break,</w:t></w:r><w:r><w:br w:type=\"page\"/></w:r><w:r><w:t>and after it.</w:t></w:r></w:p>",
            "<w:p><w:r><w:br w:type=\"page\"/></w:r></w:p>", Plain("The appendix, with the fees for the year."))));
        yield return ("probe-tables", Letter("Arial", Extras.Tables, Body(Plain("Your fees for the year."), Table(), Plain("Please pay by the end of March."))));
    }

    private static byte[] Rendered(MemoryStream template) =>
        DocumentTemplate.Compile(template, new FieldDictionary([])).Document!.Render(JsonDocument.Parse("{}").RootElement, Benchmark.British).Docx!;

    private static string Body(params string[] paragraphs) => string.Concat(paragraphs);

    private static string Plain(string text) => $"<w:p><w:r><w:t xml:space=\"preserve\">{text}</w:t></w:r></w:p>";

    private static string Run(string text, string rPr) => $"<w:p><w:r><w:rPr>{rPr}</w:rPr><w:t>{text}</w:t></w:r></w:p>";

    private static string Spaced(string text, string spacing, bool contextual = false) =>
        $"<w:p><w:pPr><w:spacing {spacing}/>{(contextual ? "<w:contextualSpacing/>" : "")}</w:pPr><w:r><w:t>{text}</w:t></w:r></w:p>";

    private static string InFont(string font, int size, string line)
    {
        var rPr = $"<w:rPr><w:rFonts w:ascii=\"{font}\" w:hAnsi=\"{font}\"/><w:sz w:val=\"{size}\"/></w:rPr>";
        return $"<w:p><w:pPr><w:spacing w:before=\"0\" w:after=\"0\" {line}/>{rPr}</w:pPr><w:r>{rPr}<w:t>{font} at {size / 2} points, a line set to show where it sits.</w:t></w:r></w:p>";
    }

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
    private static MemoryStream Letter(string font, Extras extras, string body)
    {
        var styles = $"<w:styles {W}><w:docDefaults><w:rPrDefault><w:rPr><w:rFonts w:asciiTheme=\"minorHAnsi\" w:hAnsiTheme=\"minorHAnsi\"/><w:sz w:val=\"22\"/></w:rPr></w:rPrDefault>"
            + "<w:pPrDefault><w:pPr><w:spacing w:after=\"160\" w:line=\"259\" w:lineRule=\"auto\"/></w:pPr></w:pPrDefault></w:docDefaults>"
            + "<w:style w:type=\"paragraph\" w:default=\"1\" w:styleId=\"Normal\"><w:name w:val=\"Normal\"/></w:style>"
            + "<w:style w:type=\"paragraph\" w:styleId=\"ListParagraph\"><w:name w:val=\"List Paragraph\"/><w:basedOn w:val=\"Normal\"/><w:pPr><w:ind w:left=\"720\"/><w:contextualSpacing/></w:pPr></w:style>"
            + (extras.HasFlag(Extras.Tables) ? RandomTables.Styles : "") + "</w:styles>";
        var docx = new TestDocx()
            .Part("word/styles.xml", styles, "application/vnd.openxmlformats-officedocument.wordprocessingml.styles+xml").Relationship($"{Rel}/styles", "styles.xml", external: false)
            .Part("word/theme/theme1.xml", TestTheme.Xml(font, font), "application/vnd.openxmlformats-officedocument.theme+xml").Relationship($"{Rel}/theme", "theme/theme1.xml", external: false);
        if (extras.HasFlag(Extras.Sum))
        {
            // As a document from an older Word saves it: the space between paragraphs is the sum of the two, not the larger.
            docx.Part("word/settings.xml", $"<w:settings {W}><w:compat><w:doNotUseHTMLParagraphAutoSpacing/></w:compat></w:settings>", "application/vnd.openxmlformats-officedocument.wordprocessingml.settings+xml")
                .Relationship($"{Rel}/settings", "settings.xml", external: false);
        }

        if (extras.HasFlag(Extras.Lists))
        {
            docx.Part("word/numbering.xml", $"<w:numbering {W}>{RandomLists.Definitions}{RandomLists.Lists}</w:numbering>", "application/vnd.openxmlformats-officedocument.wordprocessingml.numbering+xml")
                .Relationship($"{Rel}/numbering", "numbering.xml", external: false);
        }

        docx.RawParagraph(body);
        return docx.Build();
    }

    /// <summary>What a probe letter has besides its text: the net's lists, Word's Table Grid, or older Word's spacing.</summary>
    [Flags]
    private enum Extras
    {
        None = 0,
        Lists = 1,
        Tables = 2,
        Sum = 4,
    }
}
