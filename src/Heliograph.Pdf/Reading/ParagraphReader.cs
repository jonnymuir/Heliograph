using System.Xml.Linq;
using static Heliograph.Pdf.Reading.WordXml;

namespace Heliograph.Pdf.Reading;

/// <summary>Reads a paragraph: its resolved style, its text, tabs, breaks and fields, and its list label.</summary>
internal sealed class ParagraphReader(StyleSheet styles, NumberingDefinitions numbering, double defaultTab)
{
    public Paragraph Read(XElement p)
    {
        var pPr = p.Element(W + "pPr");
        var chain = styles.Chain(Val(pPr?.Element(W + "pStyle")) ?? styles.DefaultParagraph);
        var numPr = pPr?.Element(W + "numPr") ?? chain.Select(s => s.Element(W + "pPr")?.Element(W + "numPr")).LastOrDefault(n => n is not null);
        var level = numbering.Level(numPr);

        var paragraphLayers = new List<XElement?> { styles.DefaultParagraphProperties };
        paragraphLayers.AddRange(chain.Select(s => s.Element(W + "pPr")));
        paragraphLayers.Add(level?.Element(W + "pPr"));
        paragraphLayers.Add(pPr);
        var style = PropertyReader.Paragraph(paragraphLayers.OfType<XElement>().ToList()) with { DefaultTab = defaultTab };

        var runLayers = new List<XElement?> { styles.DefaultRun };
        runLayers.AddRange(chain.Select(s => s.Element(W + "rPr")));

        TextInline? label = null;
        if (level is not null && numbering.NextLabel(numPr!, level) is { } text)
        {
            // A bullet's own font is usually Symbol, which is not built in; the bullet character is drawn in the text font.
            label = new TextInline(text, PropertyReader.Run([.. runLayers, level.Element(W + "rPr")]) with { Font = "" });
        }

        var mark = PropertyReader.Run([.. runLayers, pPr?.Element(W + "rPr")]);
        return new Paragraph(style, Inlines(p, runLayers), label, mark);
    }

    private List<Inline> Inlines(XElement p, List<XElement?> runLayers)
    {
        var inlines = new List<Inline>();
        var field = new FieldState();
        foreach (var r in p.Descendants().Where(d => d.Name == W + "r" || d.Name == W + "fldSimple"))
        {
            if (r.Name == W + "fldSimple")
            {
                inlines.Add(new FieldInline(FieldState.Name((string?)r.Attribute(W + "instr")), PropertyReader.Run(runLayers)));
            }
            else if (!r.Ancestors(W + "fldSimple").Any())
            {
                Run(r, Style(r, runLayers), field, inlines);
            }
        }

        return inlines;
    }

    private RunStyle Style(XElement r, List<XElement?> runLayers)
    {
        var rPr = r.Element(W + "rPr");
        var layers = new List<XElement?>(runLayers);
        if (Val(rPr?.Element(W + "rStyle")) is { } characterStyle)
        {
            layers.AddRange(styles.Chain(characterStyle).Select(s => s.Element(W + "rPr")));
        }

        layers.Add(rPr);
        return PropertyReader.Run(layers);
    }

    private static void Run(XElement r, RunStyle style, FieldState field, List<Inline> inlines)
    {
        foreach (var c in r.Elements())
        {
            if (field.Consume(c, style, inlines))
            {
                continue;
            }

            if (c.Name == W + "t")
            {
                inlines.Add(new TextInline(c.Value, style));
            }
            else if (c.Name == W + "tab")
            {
                inlines.Add(new TabInline(style));
            }
            else if (c.Name == W + "br" || c.Name == W + "cr")
            {
                inlines.Add(new BreakInline(style));
            }
        }
    }

    /// <summary>
    /// A complex field runs from a begin mark, through its instruction, to a separate mark, then its last result, to an end
    /// mark. The field is kept by name and its stored result skipped, so a page number is worked out when laid out.
    /// </summary>
    private sealed class FieldState
    {
        private string? _instruction;
        private bool _inResult;

        public static string Name(string? instruction) =>
            (instruction ?? "").Trim().Split(' ', 2)[0].ToUpperInvariant();

        public bool Consume(XElement c, RunStyle style, List<Inline> inlines)
        {
            if (c.Name == W + "fldChar")
            {
                switch ((string?)c.Attribute(W + "fldCharType"))
                {
                    case "begin":
                        _instruction = "";
                        break;
                    case "separate":
                        inlines.Add(new FieldInline(Name(_instruction), style));
                        _inResult = true;
                        break;
                    case "end":
                        _instruction = null;
                        _inResult = false;
                        break;
                }

                return true;
            }

            if (c.Name == W + "instrText" && _instruction is not null)
            {
                _instruction += c.Value;
                return true;
            }

            return _inResult;
        }
    }
}
