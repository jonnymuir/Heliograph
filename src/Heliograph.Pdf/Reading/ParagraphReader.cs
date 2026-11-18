using System.Xml.Linq;
using static Heliograph.Pdf.Reading.WordXml;

namespace Heliograph.Pdf.Reading;

/// <summary>Reads a paragraph: its resolved style, its text, tabs, breaks and fields, and its list label.</summary>
internal sealed class ParagraphReader(StyleSheet styles, NumberingDefinitions numbering, double defaultTab, Func<string, byte[]?> linked)
{
    private readonly ListItemReader _items = new(numbering);

    public Paragraph Read(XElement p)
    {
        var pPr = p.Element(W + "pPr");
        var chain = styles.Chain(Val(pPr?.Element(W + "pStyle")) ?? styles.DefaultParagraph);
        var place = numbering.Place(pPr?.Element(W + "numPr") ?? chain.Select(s => s.Element(W + "pPr")?.Element(W + "numPr")).LastOrDefault(n => n is not null));
        var level = place?.Definition;

        var paragraphLayers = new List<XElement?> { styles.DefaultParagraphProperties };
        paragraphLayers.AddRange(chain.Select(s => s.Element(W + "pPr")));
        paragraphLayers.Add(level?.Element(W + "pPr"));
        paragraphLayers.Add(pPr);
        var style = PropertyReader.Paragraph(paragraphLayers.OfType<XElement>().ToList()) with { DefaultTab = defaultTab };

        var runLayers = new List<XElement?> { styles.DefaultRun };
        runLayers.AddRange(chain.Select(s => s.Element(W + "rPr")));

        List<XElement?> markLayers = [.. runLayers, pPr?.Element(W + "rPr")];
        var paragraph = new Paragraph(style, Inlines(p, runLayers), null, PropertyReader.Run(markLayers));
        return place is null ? paragraph : _items.Listed(paragraph, place, markLayers);
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

    private void Run(XElement r, RunStyle style, FieldState field, List<Inline> inlines)
    {
        foreach (var c in r.Elements())
        {
            if (!field.Consume(c, style, inlines) && Inline(c, style) is { } inline)
            {
                inlines.Add(inline);
            }
        }
    }

    // What one element of a run adds: text, a tab, a break or a picture; null for anything else.
    private Inline? Inline(XElement c, RunStyle style) => c.Name.Namespace != W ? null : c.Name.LocalName switch
    {
        "t" => new TextInline(c.Value, style),
        "tab" => new TabInline(style),
        "br" or "cr" => new BreakInline(style),
        "drawing" => PictureReader.Read(c, linked, style),
        _ => null,
    };

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
