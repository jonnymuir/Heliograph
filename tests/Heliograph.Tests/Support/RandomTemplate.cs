using System.Globalization;
using System.Text;
using System.Xml.Linq;

namespace Heliograph.Tests.Support;

/// <summary>
/// Builds a document nobody would write on purpose: paragraphs and tables with tags and conditions dropped anywhere,
/// well placed or not, and the text chopped into runs at random. The same seed always builds the same document.
/// </summary>
internal sealed class RandomTemplate
{
    public static readonly string[] YesNoFields = ["Owes money", "Is a member", "Has a fire act"];

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

    private static readonly string[] Words = ["Dear", "you", "owe", "us", "please", "pay", "by", "Friday", "the", "licence", "is", "ready", "."];
    private static readonly string[] Values = ["((Applicant name))", "((Notes))", "£((Amount owed))", "((Applicant address))"];
    private static readonly string[] Formats = ["", "<w:rPr><w:b/></w:rPr>", "<w:rPr><w:i/></w:rPr>", "<w:rPr><w:u w:val=\"single\"/></w:rPr>"];

    private readonly Random _random;

    private RandomTemplate(int seed) => _random = new Random(seed);

    /// <summary>The data for one recipient: every field supplied, with the yes or no fields set from the bits of <paramref name="answers"/>.</summary>
    public static string Data(int answers) => string.Create(CultureInfo.InvariantCulture, $$"""
        {
          "owesMoney": {{Bit(answers, 0)}}, "isAMember": {{Bit(answers, 1)}}, "hasAFireAct": {{Bit(answers, 2)}},
          "applicantName": "Alex Applicant", "notes": "First paragraph.\n\nSecond paragraph.\nSame paragraph.",
          "amountOwed": 12.5, "applicantAddress": ["12 Tumble Lane", "Clubtown", "ZZ1 1ZZ"]
        }
        """);

    public static TestDocx Build(int seed) => new TestDocx().RawParagraph(new RandomTemplate(seed).Blocks(depth: 0));

    /// <summary>Chops the text of every paragraph in a document's body into runs at random places, with random formatting.</summary>
    public static byte[] Chopped(byte[] docx, int seed)
    {
        XNamespace w = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";
        var random = new Random(seed);
        return TestDocx.WithPart(docx, "word/document.xml", document =>
        {
            foreach (var run in document.Descendants(w + "r").Where(r => r.Elements(w + "t").Count() == 1 && r.Elements().All(e => e.Name == w + "t" || e.Name == w + "rPr")).ToList())
            {
                var text = run.Element(w + "t")!.Value;
                var pieces = Pieces(text, random);
                run.ReplaceWith(pieces.Select(piece => new XElement(w + "r", run.Element(w + "rPr") is { } format && random.Next(3) > 0 ? new XElement(format) : null,
                    new XElement(w + "t", new XAttribute(XNamespace.Xml + "space", "preserve"), piece))));
            }
        });
    }

    private static string Bit(int answers, int bit) => (answers >> bit & 1) == 1 ? "true" : "false";

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

    private string Blocks(int depth)
    {
        var xml = new StringBuilder();
        var count = _random.Next(1, depth == 0 ? 7 : 3);
        for (var index = 0; index < count; index++)
        {
            xml.Append(_random.Next(40) switch
            {
                // The mistakes: a condition tag on its own line with no partner.
                0 => Paragraph($"((if {Pick(YesNoFields)}))"),
                1 => Paragraph("((end if))"),
                < 10 when depth < 3 => Paragraph($"((if {Pick(YesNoFields)}))") + Blocks(depth + 1) + Paragraph("((end if))"),
                < 14 when depth < 2 => Table(depth),
                _ => Paragraph(Sentence(depth: 0)),
            });
        }

        return xml.ToString();
    }

    private string Table(int depth)
    {
        var rows = new StringBuilder();
        for (var row = _random.Next(1, 3); row > 0; row--)
        {
            rows.Append("<w:tr>");
            for (var cell = _random.Next(1, 3); cell > 0; cell--)
            {
                // Sometimes the whole cell is inside one condition, which is what empties a cell.
                var content = _random.Next(3) == 0
                    ? Paragraph($"((if {Pick(YesNoFields)}))") + Blocks(depth + 2) + Paragraph("((end if))")
                    : Blocks(depth + 2);
                rows.Append("<w:tc><w:tcPr><w:tcW w:w=\"2000\" w:type=\"dxa\"/></w:tcPr>").Append(content).Append("</w:tc>");
            }

            rows.Append("</w:tr>");
        }

        return $"<w:tbl><w:tblPr><w:tblW w:w=\"0\" w:type=\"auto\"/></w:tblPr><w:tblGrid><w:gridCol w:w=\"2000\"/><w:gridCol w:w=\"2000\"/></w:tblGrid>{rows}</w:tbl>";
    }

    private string Sentence(int depth)
    {
        var text = new StringBuilder();
        for (var count = _random.Next(depth == 0 ? 1 : 0, 6); count > 0; count--)
        {
            text.Append(_random.Next(60) switch
            {
                // The mistakes: a condition tag in a sentence with no partner in it.
                0 => $"((if {Pick(YesNoFields)}))",
                1 => "((end if))",
                < 14 => Pick(Values),
                < 24 when depth < 3 => $"((if {Pick(YesNoFields)})){Sentence(depth + 1)}((end if))",
                _ => Pick(Words),
            }).Append(' ');
        }

        return text.ToString();
    }

    private string Paragraph(string text) => "<w:p>" + string.Concat(Pieces(text, _random).Select(piece =>
        $"<w:r>{Pick(Formats)}<w:t xml:space=\"preserve\">{new XText(piece)}</w:t></w:r>")) + "</w:p>";

    private string Pick(string[] from) => from[_random.Next(from.Length)];
}
