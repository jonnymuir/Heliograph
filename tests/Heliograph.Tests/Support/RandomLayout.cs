using System.Globalization;
using System.Text;

namespace Heliograph.Tests.Support;

/// <summary>
/// Builds a document to test layout, not tags: paragraphs of every length, headings, lists, alignment, indents, line
/// spacing, keep rules, page breaks, tables with spans and repeated heading rows, headers and footers with page numbers,
/// over several pages. It uses only what Heliograph's own PDF converter claims to lay out, so the two PDF routes can be
/// compared line for line. The same seed always builds the same document.
/// </summary>
internal sealed class RandomLayout
{
    private const string W = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";
    private const string R = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
    private const string Rel = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";

    private static readonly string[] Vocabulary =
    [
        "the", "of", "and", "a", "to", "in", "is", "you", "that", "it", "your", "licence", "application", "contribution", "member",
        "statement", "we", "will", "for", "on", "are", "with", "as", "this", "be", "at", "by", "not", "or", "have", "from",
        "juggling", "federation", "performance", "Clubtown", "December", "approved", "reference", "payment", "£45.00", "2027",
        "information", "responsibility", "notwithstanding", "Featherstonehaugh-Cholmondeley", "a.m.", "e.g.", "(see", "below)",
        "12", "3.50", "it's", "don't", "O'Hare", "well-known", "telephone:", "0300", "123", "4567", "—", "café", "naïve",
    ];

    private static readonly string[] Fonts = ["Arial", "Times New Roman", "Courier New", "Liberation Sans"];
    private static readonly string[] Alignments = ["left", "left", "both", "center", "right"];
    private static readonly string[] Sides = ["top", "left", "bottom", "right"];

    private readonly Random _random;
    private readonly Random _lists;
    private readonly Random _pictures;
    private readonly Random _formatting;
    private readonly Random _tabs;
    private readonly Random _spacingRules;
    private readonly bool _mostlyLists;
    private readonly StringBuilder _body = new();

    /// <summary>A document of every kind of block, or, with <see cref="LayoutMix.MostlyLists"/>, one where most blocks are lists with levels.</summary>
    public RandomLayout(int seed, LayoutMix mix = LayoutMix.Everything)
    {
        _mostlyLists = mix == LayoutMix.MostlyLists;
        _random = new Random(seed);
        // Lists drawn from their own stream, so what else a seed builds stays as it was before lists grew levels.
        _lists = new Random(seed + 10_000);
        _pictures = new Random(seed + 20_000);
        _formatting = new Random(seed + 40_000);
        _tabs = new Random(seed + 50_000);
        _spacingRules = new Random(seed + 60_000);
        // Odd seeds are saved as a new Word document is, without doNotUseHTMLParagraphAutoSpacing. Taken from the seed, not
        // drawn, so every other choice for a seed stays as it was.
        LargerSpacing = seed % 2 == 1;
        (Font, FromTheme) = Looks(Fonts[_random.Next(Fonts.Length)], seed);
        Size = new[] { 20, 22, 24 }[_random.Next(3)];
        TitlePage = _random.Next(3) == 0;
        Width = _random.Next(2) == 0 ? 11906 : 12240;
        Height = Width == 11906 ? 16838 : 15840;
        Margin = new[] { 720, 1134, 1440, 1800 }[_random.Next(4)];
        DefaultTab = _random.Next(2) == 0 ? 720 : 709;
        var blocks = _random.Next(15, 70);
        for (var i = 0; i < blocks; i++)
        {
            Block();
        }

        _body.Append(SectionXml());
    }

    private string Font { get; }
    private int Size { get; }
    private bool TitlePage { get; }
    private int Width { get; }
    private int Height { get; }
    private int Margin { get; }
    private int DefaultTab { get; }

    private bool LargerSpacing { get; }

    private bool FromTheme { get; }

    // A third of documents are set in Calibri or Cambria, half of those through the theme as Word writes them; drawn from
    // their own stream so each seed's other choices stay as they were.
    private static (string Font, bool FromTheme) Looks(string font, int seed)
    {
        var looks = new Random(seed + 30_000);
        return looks.Next(3) == 0 ? (looks.Next(2) == 0 ? "Calibri" : "Cambria", looks.Next(2) == 0) : (font, false);
    }
    private int ContentWidth => Width - (2 * Margin);

    public byte[] Docx()
    {
        var docx = new TestDocx().RawParagraph(_body.ToString())
            .Part("word/styles.xml", StylesXml(), "application/vnd.openxmlformats-officedocument.wordprocessingml.styles+xml")
            .Part("word/numbering.xml", NumberingXml(), "application/vnd.openxmlformats-officedocument.wordprocessingml.numbering+xml")
            .Part("word/settings.xml", SettingsXml(), "application/vnd.openxmlformats-officedocument.wordprocessingml.settings+xml")
            .Part("word/header1.xml", Story("hdr", Paragraph("National Juggling Federation", bold: true) + Paragraph(Words(4, 10))), "application/vnd.openxmlformats-officedocument.wordprocessingml.header+xml")
            .Part("word/footer1.xml", Story("ftr", PageNumberParagraph()), "application/vnd.openxmlformats-officedocument.wordprocessingml.footer+xml")
            .Part("word/header2.xml", Story("hdr", Paragraph("First page " + Words(2, 5))), "application/vnd.openxmlformats-officedocument.wordprocessingml.header+xml")
            .Relationship($"{Rel}/styles", "styles.xml", external: false)
            .Relationship($"{Rel}/numbering", "numbering.xml", external: false)
            .Relationship($"{Rel}/settings", "settings.xml", external: false)
            .Relationship($"{Rel}/header", "header1.xml", external: false)
            .Relationship($"{Rel}/footer", "footer1.xml", external: false)
            .Relationship($"{Rel}/header", "header2.xml", external: false)
            .Media("word/media/picture.png", RandomPictures.Png, "image/png")
            .Relationship($"{Rel}/image", "media/picture.png", external: false)
            .Part("word/theme/theme1.xml", TestTheme.Xml(Font, Font), "application/vnd.openxmlformats-officedocument.theme+xml")
            .Relationship($"{Rel}/theme", "theme/theme1.xml", external: false);
        return docx.Build().ToArray();
    }

    // ---- blocks ----

    private void Block()
    {
        var kind = Kind();
        if (kind < 8)
        {
            _body.Append(Paragraph(Words(2, 8), style: _random.Next(2) == 0 ? "Heading1" : "Heading2"));
        }
        else if (kind < 16)
        {
            List();
        }
        else if (kind < 24)
        {
            Table();
        }
        else if (kind < 27)
        {
            _body.Append(Paragraph(""));
        }
        else
        {
            _body.Append(PlainParagraph());
        }
    }

    // An ordinary paragraph: its spacing, indents, alignment and tabs, formatted runs, sometimes a picture. A fifth leave out
    // space between paragraphs of the same style, as Word's List Paragraph does.
    private string PlainParagraph()
    {
        var properties = ParagraphProperties();
        properties = _spacingRules.Next(5) == 0 ? properties.Replace("<w:jc ", "<w:contextualSpacing/><w:jc ", StringComparison.Ordinal) : properties;
        var aligned = properties.Contains("w:val=\"center\"", StringComparison.Ordinal) || properties.Contains("w:val=\"right\"", StringComparison.Ordinal);
        var paragraph = RandomFormatting.Into(AlignedTabs(Paragraph(Words(1, 120), properties: properties, tabs: !aligned)), _formatting);
        return _pictures.Next(8) == 0 ? RandomPictures.Into(paragraph, _pictures) : paragraph;
    }

    private string ParagraphProperties()
    {
        var p = new StringBuilder();
        if (_random.Next(25) == 0)
        {
            p.Append("<w:pageBreakBefore/>");
        }

        if (_random.Next(6) == 0)
        {
            p.Append(_random.Next(2) == 0 ? "<w:keepNext/>" : "<w:keepLines/>");
        }

        if (_random.Next(10) == 0)
        {
            p.Append("<w:widowControl w:val=\"0\"/>");
        }

        if (_random.Next(5) == 0)
        {
            p.Append(Invariant($"<w:tabs><w:tab w:val=\"left\" w:pos=\"{_random.Next(1, 8) * 567}\"/></w:tabs>"));
        }

        var line = _random.Next(6) switch
        {
            0 => "w:line=\"276\" w:lineRule=\"auto\"",
            1 => "w:line=\"360\" w:lineRule=\"auto\"",
            2 => "w:line=\"300\" w:lineRule=\"exact\"",
            3 => "w:line=\"320\" w:lineRule=\"atLeast\"",
            _ => "",
        };
        p.Append(Invariant($"<w:spacing w:before=\"{_random.Next(4) * 60}\" w:after=\"{_random.Next(4) * 80}\" {line}/>"));
        if (_random.Next(3) == 0)
        {
            var left = _random.Next(4) * 360;
            var first = _random.Next(3) switch { 0 => Invariant($" w:firstLine=\"{_random.Next(1, 3) * 360}\""), 1 when left > 0 => Invariant($" w:hanging=\"{Math.Min(left, 360)}\""), _ => "" };
            p.Append(Invariant($"<w:ind w:left=\"{left}\" w:right=\"{_random.Next(3) * 360}\"{first}/>"));
        }

        p.Append(Invariant($"<w:jc w:val=\"{Alignments[_random.Next(Alignments.Length)]}\"/>"));
        return p.ToString();
    }

    // A list with levels steps up or down a level, or stays, from one item to the next.
    private int NextLevel(int level, bool rich) => rich ? Math.Clamp(level + _lists.Next(-1, 2), 0, 2) : 0;

    // Half the paragraphs' tab stops line text up on their right, centre or decimal point, some with a leader.
    private string AlignedTabs(string paragraph)
    {
        const string Left = "<w:tab w:val=\"left\" ";
        if (!paragraph.Contains(Left, StringComparison.Ordinal) || _tabs.Next(2) == 0)
        {
            return paragraph;
        }

        var align = new[] { "right", "center", "decimal" }[_tabs.Next(3)];
        var leader = new[] { "", "", "w:leader=\"dot\" ", "w:leader=\"hyphen\" ", "w:leader=\"underscore\" " }[_tabs.Next(5)];
        return paragraph.Replace(Left, $"<w:tab w:val=\"{align}\" {leader}", StringComparison.Ordinal);
    }

    // What block comes next, out of 100: a document made mostly of lists turns most draws into a list (8 to 15).
    private int Kind()
    {
        var kind = _random.Next(100);
        return _mostlyLists && kind < 70 ? 8 : kind;
    }

    // Half the lists are a plain bullet or number list; the rest use RandomLists' definitions, with items that step
    // between levels.
    private void List()
    {
        var plain = _random.Next(1, 3);
        var rich = _mostlyLists || _lists.Next(2) == 0;
        var numId = rich ? RandomLists.Pick(_lists) : plain;
        var level = 0;
        for (var i = _random.Next(2, 7); i > 0; i--)
        {
            _body.Append(Paragraph(Words(2, 40), properties: Invariant($"<w:numPr><w:ilvl w:val=\"{level}\"/><w:numId w:val=\"{numId}\"/></w:numPr>")));
            level = NextLevel(level, rich);
        }
    }

    private void Table()
    {
        var columns = _random.Next(2, 6);
        var widths = Enumerable.Range(0, columns).Select(_ => _random.Next(1, 4)).ToArray();
        var total = widths.Sum();
        var grid = widths.Select(w => (int)(ContentWidth * 0.95 * w / total)).ToArray();
        var borders = _random.Next(4) != 0;
        var table = new StringBuilder($"<w:tbl><w:tblPr><w:tblInd w:w=\"{_random.Next(3) * 50}\" w:type=\"dxa\"/>");
        if (_random.Next(2) == 0)
        {
            table.Append("<w:tblCellMar><w:left w:w=\"100\" w:type=\"dxa\"/><w:right w:w=\"100\" w:type=\"dxa\"/></w:tblCellMar>");
        }

        table.Append("</w:tblPr><w:tblGrid>").Append(string.Concat(grid.Select(g => Invariant($"<w:gridCol w:w=\"{g}\"/>")))).Append("</w:tblGrid>");
        var header = _random.Next(2) == 0;
        var rows = _random.Next(1, 30);
        for (var r = 0; r < rows; r++)
        {
            table.Append("<w:tr>").Append(r == 0 && header ? "<w:trPr><w:tblHeader/></w:trPr>" : "");
            for (var c = 0; c < columns;)
            {
                var span = c < columns - 1 && _random.Next(8) == 0 ? 2 : 1;
                table.Append(Cell(grid.Skip(c).Take(span).Sum(), span, borders, bold: r == 0 && header));
                c += span;
            }

            table.Append("</w:tr>");
        }

        _body.Append(table).Append("</w:tbl>").Append(Paragraph(""));
    }

    private string Cell(int width, int span, bool borders, bool bold)
    {
        var properties = Invariant($"<w:tcW w:w=\"{width}\" w:type=\"dxa\"/>") + (span > 1 ? Invariant($"<w:gridSpan w:val=\"{span}\"/>") : "");
        if (borders)
        {
            properties += "<w:tcBorders>" + string.Concat(Sides.Select(side => $"<w:{side} w:val=\"single\" w:sz=\"4\" w:space=\"0\" w:color=\"000000\"/>")) + "</w:tcBorders>";
        }

        var align = _random.Next(3) == 0 ? "<w:jc w:val=\"right\"/>" : "";
        return $"<w:tc><w:tcPr>{properties}</w:tcPr>{Paragraph(Words(1, 15), bold: bold, properties: "<w:spacing w:before=\"0\" w:after=\"0\"/>" + align)}</w:tc>";
    }

    // ---- text ----

    private string Words(int min, int max)
    {
        var words = Enumerable.Range(0, _random.Next(min, max + 1)).Select(_ => Vocabulary[_random.Next(Vocabulary.Length)]);
        return string.Join(' ', words);
    }

    private string Paragraph(string text, string? style = null, string properties = "", bool bold = false, bool tabs = false)
    {
        var pPr = (style is null ? "" : $"<w:pStyle w:val=\"{style}\"/>") + properties;
        var runs = new StringBuilder();
        foreach (var piece in Pieces(text, tabs))
        {
            runs.Append(piece);
        }

        var rPr = bold ? "<w:rPr><w:b/></w:rPr>" : "";
        return $"<w:p><w:pPr>{pPr}</w:pPr>{(bold ? runs.ToString().Replace("<w:r>", $"<w:r>{rPr}", StringComparison.Ordinal) : runs)}</w:p>";
    }

    // The text in runs of a few words, some bold or italic, with the odd line break or tab between them.
    private IEnumerable<string> Pieces(string text, bool tabs)
    {
        var words = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        for (var i = 0; i < words.Length;)
        {
            var take = Math.Min(words.Length - i, _random.Next(1, 8));
            var chunk = string.Join(' ', words.Skip(i).Take(take)) + (i + take < words.Length ? " " : "");
            i += take;
            var format = _random.Next(10) switch { 0 => "<w:rPr><w:b/></w:rPr>", 1 => "<w:rPr><w:i/></w:rPr>", _ => "" };
            var between = i < words.Length ? _random.Next(30) switch { 0 => "<w:br/>", 1 when tabs => "<w:tab/>", _ => "" } : "";
            yield return $"<w:r>{format}<w:t xml:space=\"preserve\">{System.Security.SecurityElement.Escape(chunk)}</w:t>{between}</w:r>";
        }
    }

    private static string PageNumberParagraph() =>
        "<w:p><w:r><w:t xml:space=\"preserve\">Page </w:t></w:r><w:r><w:fldChar w:fldCharType=\"begin\"/></w:r><w:r><w:instrText> PAGE </w:instrText></w:r>"
        + "<w:r><w:fldChar w:fldCharType=\"separate\"/></w:r><w:r><w:t>1</w:t></w:r><w:r><w:fldChar w:fldCharType=\"end\"/></w:r>"
        + "<w:r><w:t xml:space=\"preserve\"> of </w:t></w:r><w:fldSimple w:instr=\" NUMPAGES \"><w:r><w:t>1</w:t></w:r></w:fldSimple></w:p>";

    // ---- parts ----

    private string SectionXml()
    {
        var first = TitlePage ? $"<w:headerReference w:type=\"first\" r:id=\"rIdX5\" xmlns:r=\"{R}\"/><w:titlePg/>" : "";
        return $"<w:sectPr xmlns:r=\"{R}\"><w:headerReference w:type=\"default\" r:id=\"rIdX3\"/><w:footerReference w:type=\"default\" r:id=\"rIdX4\"/>"
            + first.Replace("<w:titlePg/>", "", StringComparison.Ordinal)
            + Invariant($"<w:pgSz w:w=\"{Width}\" w:h=\"{Height}\"/><w:pgMar w:top=\"{Margin}\" w:right=\"{Margin}\" w:bottom=\"{Margin}\" w:left=\"{Margin}\" w:header=\"708\" w:footer=\"708\" w:gutter=\"0\"/>")
            + (TitlePage ? "<w:titlePg/>" : "") + "</w:sectPr>";
    }

    // The compatibility settings LibreOffice and Microsoft 365 write (see the benchmark letters): space between paragraphs
    // is the sum of one's space after and the next one's space before. A new Word document leaves out
    // doNotUseHTMLParagraphAutoSpacing, and then it is the larger of the two (tests/Heliograph.Tests/Fixtures/Word).
    private string SettingsXml() => Invariant($"<w:settings xmlns:w=\"{W}\"><w:defaultTabStop w:val=\"{DefaultTab}\"/><w:compat><w:adjustLineHeightInTable/>")
        + (LargerSpacing ? "" : "<w:doNotUseHTMLParagraphAutoSpacing/>") + "<w:compatSetting w:name=\"compatibilityMode\" w:uri=\"http://schemas.microsoft.com/office/word\" w:val=\"12\"/></w:compat></w:settings>";

    private static string Story(string root, string paragraphs) => $"<w:{root} xmlns:w=\"{W}\">{paragraphs}</w:{root}>";

    private string FontsXml => FromTheme
        ? "<w:rFonts w:asciiTheme=\"minorHAnsi\" w:hAnsiTheme=\"minorHAnsi\"/>"
        : $"<w:rFonts w:ascii=\"{Font}\" w:hAnsi=\"{Font}\" w:cs=\"{Font}\"/>";

    private string StylesXml() => $"""
        <w:styles xmlns:w="{W}">
          <w:docDefaults><w:rPrDefault><w:rPr>{FontsXml}<w:sz w:val="{Size}"/><w:lang w:val="en-GB"/></w:rPr></w:rPrDefault>
          <w:pPrDefault><w:pPr><w:spacing w:after="160"/></w:pPr></w:pPrDefault></w:docDefaults>
          <w:style w:type="paragraph" w:default="1" w:styleId="Normal"><w:name w:val="Normal"/></w:style>
          <w:style w:type="paragraph" w:styleId="Heading1"><w:name w:val="heading 1"/><w:basedOn w:val="Normal"/><w:pPr><w:keepNext/><w:spacing w:before="240" w:after="120"/><w:outlineLvl w:val="0"/></w:pPr><w:rPr><w:b/><w:sz w:val="36"/></w:rPr></w:style>
          <w:style w:type="paragraph" w:styleId="Heading2"><w:name w:val="heading 2"/><w:basedOn w:val="Normal"/><w:pPr><w:keepNext/><w:spacing w:before="200" w:after="100"/><w:outlineLvl w:val="1"/></w:pPr><w:rPr><w:b/><w:sz w:val="28"/></w:rPr></w:style>
        </w:styles>
        """;

    private static string NumberingXml() => $"""
        <w:numbering xmlns:w="{W}">
          <w:abstractNum w:abstractNumId="0"><w:lvl w:ilvl="0"><w:start w:val="1"/><w:numFmt w:val="bullet"/><w:lvlText w:val="•"/><w:lvlJc w:val="left"/><w:pPr><w:ind w:left="720" w:hanging="360"/></w:pPr></w:lvl></w:abstractNum>
          <w:abstractNum w:abstractNumId="1"><w:lvl w:ilvl="0"><w:start w:val="1"/><w:numFmt w:val="decimal"/><w:lvlText w:val="%1."/><w:lvlJc w:val="left"/><w:pPr><w:ind w:left="720" w:hanging="360"/></w:pPr></w:lvl></w:abstractNum>
          <w:num w:numId="1"><w:abstractNumId w:val="0"/></w:num>
          <w:num w:numId="2"><w:abstractNumId w:val="1"/></w:num>
        {RandomLists.Definitions}
        </w:numbering>
        """;

    private static string Invariant(FormattableString text) => text.ToString(CultureInfo.InvariantCulture);
}

/// <summary>What a random layout is made of.</summary>
internal enum LayoutMix
{
    Everything,
    MostlyLists,
}
