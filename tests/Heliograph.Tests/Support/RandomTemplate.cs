using System.Globalization;
using System.Text;
using System.Xml.Linq;

namespace Heliograph.Tests.Support;

/// <summary>
/// Builds a document nobody would write on purpose: paragraphs and tables, conditions around and inside them to any
/// depth, values everywhere, and the text chopped into runs at random. It also knows what the finished letter should
/// say for any recipient, worked out from its own plan and not from the engine, so a test can compare the two.
/// The same seed always builds the same document.
/// </summary>
internal sealed class RandomTemplate
{
    public static readonly FieldDictionary Dictionary = new(
    [
        new Field("Owes money", "owesMoney", FieldType.YesNo),
        new Field("Is a member", "isAMember", FieldType.YesNo),
        new Field("Has a fire act", "hasAFireAct", FieldType.YesNo),
        new Field("Applicant name", "applicantName", FieldType.Text),
        new Field("Notes", "notes", FieldType.Text),
        new Field("Amount owed", "amountOwed", FieldType.Money),
        new Field("Applicant address", "applicantAddress", FieldType.Address),
    ]);

    private const char NewParagraph = (char)0x2029;
    private static readonly string[] YesNoFields = ["Owes money", "Is a member", "Has a fire act"];
    private static readonly string[] Words = ["Dear", "you", "owe", "us", "please", "pay", "by", "Friday", "the", "licence", "is", "ready", "."];
    private static readonly string[] Formats = ["", "<w:rPr><w:b/></w:rPr>", "<w:rPr><w:i/></w:rPr>", "<w:rPr><w:u w:val=\"single\"/></w:rPr>"];

    // Each tag an author can print, and what it should become for the recipient in Data.
    private static readonly (string Tag, string Printed)[] Values =
    [
        ("((Applicant name))", "Alex Applicant"),
        ("((Notes))", $"First paragraph.{NewParagraph}Second paragraph.\nSame paragraph."),
        ("£((Amount owed))", "£12.50"),
        ("((Applicant address))", "12 Tumble Lane\nClubtown\nZZ1 1ZZ"),
    ];

    private readonly Random _random;
    private readonly List<Block> _blocks;

    public RandomTemplate(int seed)
    {
        _random = new Random(seed);

        // One template in three is given a single mistake: a condition tag with no partner, or with one in the wrong place.
        MistakeAt = _random.Next(3) == 0 ? _random.Next(1, 12) : 0;
        _blocks = Blocks(depth: 0);
        HasMistake = MistakeAt > 0 && _choices >= MistakeAt;
    }

    private int MistakeAt { get; }
    private int _choices;

    /// <summary>Whether the document holds a misplaced condition tag, and so must be refused.</summary>
    public bool HasMistake { get; }

    /// <summary>The data for one recipient: every field supplied, with the yes or no fields set from the bits of <paramref name="answers"/>.</summary>
    public static string Data(int answers) => string.Create(CultureInfo.InvariantCulture, $$"""
        {
          "owesMoney": {{(Yes(answers, "Owes money") ? "true" : "false")}}, "isAMember": {{(Yes(answers, "Is a member") ? "true" : "false")}},
          "hasAFireAct": {{(Yes(answers, "Has a fire act") ? "true" : "false")}},
          "applicantName": "Alex Applicant", "notes": "First paragraph.\n\nSecond paragraph.\nSame paragraph.",
          "amountOwed": 12.5, "applicantAddress": ["12 Tumble Lane", "Clubtown", "ZZ1 1ZZ"]
        }
        """);

    public byte[] Docx() => new TestDocx().RawParagraph(string.Concat(_blocks.Select(block => block.Xml(ParagraphXml)))).Build().ToArray();

    /// <summary>The text of every paragraph the finished letter should have, in order, for one recipient.</summary>
    public IReadOnlyList<string> Expected(int answers) => [.. _blocks.SelectMany(block => block.Expected(answers))];

    /// <summary>Chops the text of every paragraph in a document's body into runs at random places, with random formatting.</summary>
    public static byte[] Chopped(byte[] docx, int seed)
    {
        XNamespace w = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";
        var random = new Random(seed);
        return TestDocx.WithPart(docx, "word/document.xml", document =>
        {
            var plain = document.Descendants(w + "r")
                .Where(r => r.Elements(w + "t").Count() == 1 && r.Elements().All(e => e.Name == w + "t" || e.Name == w + "rPr")).ToList();
            foreach (var run in plain)
            {
                var format = run.Element(w + "rPr");
                run.ReplaceWith(Pieces(run.Element(w + "t")!.Value, random).Select(piece => new XElement(w + "r",
                    format is not null && random.Next(3) > 0 ? new XElement(format) : null,
                    new XElement(w + "t", new XAttribute(XNamespace.Xml + "space", "preserve"), piece))));
            }
        });
    }

    private static bool Yes(int answers, string field) => (answers >> Array.IndexOf(YesNoFields, field) & 1) == 1;

    private static List<string> Pieces(string text, Random random)
    {
        var pieces = new List<string>();
        for (var position = 0; position < text.Length;)
        {
            var length = Math.Min(text.Length - position, random.Next(1, 7));
            pieces.Add(text.Substring(position, length));
            position += length;
        }

        return pieces;
    }

    /// <summary>True once, at the place the template's one mistake goes.</summary>
    private bool Mistake() => ++_choices == MistakeAt;

    private List<Block> Blocks(int depth)
    {
        var blocks = new List<Block>();
        for (var count = _random.Next(1, depth == 0 ? 7 : 3); count > 0; count--)
        {
            blocks.Add(_random.Next(40) switch
            {
                _ when Mistake() => BlockMistake(depth),
                < 10 when depth < 3 => new Condition(Pick(YesNoFields), Blocks(depth + 1)),
                < 14 when depth < 2 => NewTable(depth),
                _ => new Paragraph(Sentence(depth: 0)),
            });
        }

        return blocks;
    }

    /// <summary>A condition tag on a line of its own with no partner, or a pair that opens inside a table and closes after it.</summary>
    private Block BlockMistake(int depth) => _random.Next(3) switch
    {
        0 => new Paragraph([new Stray($"((if {Pick(YesNoFields)}))")]),
        1 => new Paragraph([new Stray("((end if))")]),
        _ => new Straddle(Pick(YesNoFields), NewTable(Math.Max(depth, 1))),
    };

    private Table NewTable(int depth)
    {
        var rows = new List<List<List<Block>>>();
        for (var row = _random.Next(1, 3); row > 0; row--)
        {
            var cells = new List<List<Block>>();
            for (var cell = _random.Next(1, 3); cell > 0; cell--)
            {
                // Sometimes the whole cell is inside one condition, which is what empties a cell.
                cells.Add(_random.Next(3) == 0 ? [new Condition(Pick(YesNoFields), Blocks(depth + 2))] : Blocks(depth + 2));
            }

            rows.Add(cells);
        }

        return new Table(rows);
    }

    private List<Token> Sentence(int depth)
    {
        var tokens = new List<Token>();
        for (var count = _random.Next(depth == 0 ? 1 : 0, 6); count > 0; count--)
        {
            tokens.Add(_random.Next(60) switch
            {
                _ when Mistake() => new Stray(_random.Next(2) == 0 ? $"((if {Pick(YesNoFields)}))" : "((end if))"),
                < 14 => new Value(Pick(Values)),
                < 24 when depth < 3 => new Inline(Pick(YesNoFields), Sentence(depth + 1)),
                _ => new Word(Pick(Words)),
            });
        }

        return tokens;
    }

    private static string InFirstCell(string table, string paragraph)
    {
        const string cellStart = "</w:tcPr>";
        var at = table.IndexOf(cellStart, StringComparison.Ordinal) + cellStart.Length;
        return table[..at] + paragraph + table[at..];
    }

    private string ParagraphXml(string text) => "<w:p>" + string.Concat(Pieces(text, _random).Select(piece =>
        $"<w:r>{Pick(Formats)}<w:t xml:space=\"preserve\">{new XText(piece)}</w:t></w:r>")) + "</w:p>";

    private T Pick<T>(T[] from) => from[_random.Next(from.Length)];

    // ---- the plan of a document, and what it should say ----

    private abstract record Token
    {
        /// <summary>The token as the author typed it, followed by a space.</summary>
        public abstract string Written { get; }

        /// <summary>What the token becomes. <paramref name="hidden"/> is set when a condition in it was not shown.</summary>
        public abstract string Printed(int answers, ref bool hidden);
    }

    private sealed record Word(string Text) : Token
    {
        public override string Written => Text + " ";
        public override string Printed(int answers, ref bool hidden) => Written;
    }

    private sealed record Value((string Tag, string Printed) Pair) : Token
    {
        public override string Written => Pair.Tag + " ";
        public override string Printed(int answers, ref bool hidden) => Pair.Printed + " ";
    }

    private sealed record Stray(string Tag) : Token
    {
        public override string Written => Tag + " ";
        public override string Printed(int answers, ref bool hidden) => throw new InvalidOperationException("A template with a mistake has no finished letter.");
    }

    private sealed record Inline(string Field, List<Token> Tokens) : Token
    {
        public override string Written => $"((if {Field}))" + string.Concat(Tokens.Select(token => token.Written)) + "((end if)) ";

        public override string Printed(int answers, ref bool hidden)
        {
            if (!Yes(answers, Field))
            {
                hidden = true;
                return " ";
            }

            var text = new StringBuilder();
            foreach (var token in Tokens)
            {
                text.Append(token.Printed(answers, ref hidden));
            }

            return text.Append(' ').ToString();
        }
    }

    private abstract record Block
    {
        /// <summary>The block as WordprocessingML. <paramref name="paragraph"/> writes one paragraph of text.</summary>
        public abstract string Xml(Func<string, string> paragraph);

        public abstract IEnumerable<string> Expected(int answers);
    }

    private sealed record Paragraph(List<Token> Tokens) : Block
    {
        public override string Xml(Func<string, string> paragraph) => paragraph(string.Concat(Tokens.Select(token => token.Written)));

        public override IEnumerable<string> Expected(int answers)
        {
            var hidden = false;
            var text = new StringBuilder();
            foreach (var token in Tokens)
            {
                text.Append(token.Printed(answers, ref hidden));
            }

            // A sentence that was all condition leaves no paragraph behind once a condition in it is hidden.
            return hidden && string.IsNullOrWhiteSpace(text.ToString()) ? [] : text.ToString().Split(NewParagraph);
        }
    }

    private sealed record Condition(string Field, List<Block> Blocks) : Block
    {
        public override string Xml(Func<string, string> paragraph) =>
            paragraph($"((if {Field}))") + string.Concat(Blocks.Select(block => block.Xml(paragraph))) + paragraph("((end if))");

        public override IEnumerable<string> Expected(int answers) =>
            Yes(answers, Field) ? Blocks.SelectMany(block => block.Expected(answers)) : [];
    }

    /// <summary>The mistake of opening a condition in a table's first cell and closing it after the table.</summary>
    private sealed record Straddle(string Field, Table Table) : Block
    {
        public override string Xml(Func<string, string> paragraph) =>
            InFirstCell(Table.Xml(paragraph), paragraph($"((if {Field}))")) + paragraph("((end if))");

        public override IEnumerable<string> Expected(int answers) => throw new InvalidOperationException("A template with a mistake has no finished letter.");
    }

    private sealed record Table(List<List<List<Block>>> Rows) : Block
    {
        private const string Start = "<w:tbl><w:tblPr><w:tblW w:w=\"0\" w:type=\"auto\"/></w:tblPr><w:tblGrid><w:gridCol w:w=\"2000\"/><w:gridCol w:w=\"2000\"/></w:tblGrid>";
        private const string CellStart = "<w:tc><w:tcPr><w:tcW w:w=\"2000\" w:type=\"dxa\"/></w:tcPr>";

        public override string Xml(Func<string, string> paragraph) => Start + string.Concat(Rows.Select(row =>
            "<w:tr>" + string.Concat(row.Select(cell => CellStart + string.Concat(cell.Select(block => block.Xml(paragraph))) + "</w:tc>")) + "</w:tr>")) + "</w:tbl>";

        // A cell always keeps one paragraph, empty if everything in it was hidden.
        public override IEnumerable<string> Expected(int answers) => Rows.SelectMany(row => row.SelectMany(cell =>
            cell.SelectMany(block => block.Expected(answers)).DefaultIfEmpty("")));
    }
}
