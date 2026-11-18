using System.Xml.Linq;
using static Heliograph.Pdf.Reading.WordXml;

namespace Heliograph.Pdf.Reading;

/// <summary>Reads a paragraph: its resolved style, its text, tabs, breaks and fields, and its list label.</summary>
internal sealed class ParagraphReader(StyleSheet styles, NumberingDefinitions numbering, double defaultTab, PartLinks links)
{
    private readonly ListItemReader _items = new(numbering);

    /// <summary>The style of the table being read, whose paragraph and run properties come under the paragraph's own style.</summary>
    public TableStyle? Table { get; set; }

    public Paragraph Read(XElement p)
    {
        var pPr = p.Element(W + "pPr");
        var styleId = Val(pPr?.Element(W + "pStyle")) ?? styles.DefaultParagraph;
        var chain = styles.Chain(styleId);
        var place = numbering.Place(pPr?.Element(W + "numPr") ?? chain.Select(s => s.Element(W + "pPr")?.Element(W + "numPr")).LastOrDefault(n => n is not null));
        var level = place?.Definition;

        var layers = ParagraphLayers(chain, level, pPr);
        var style = PropertyReader.Paragraph(layers) with
        {
            DefaultTab = defaultTab,
            StyleId = styleId,
            Contextual = layers.LastOrDefault(l => l.Element(W + "contextualSpacing") is not null)?.Element(W + "contextualSpacing") is { } c && On(c),
            Frame = FrameReader.Read(layers),
        };

        var runLayers = RunLayers(chain);

        List<XElement?> markLayers = [.. runLayers, pPr?.Element(W + "rPr")];
        var paragraph = new Paragraph(style, Inlines(p, runLayers), null, PropertyReader.Run(markLayers));
        return place is null ? paragraph : _items.Listed(paragraph, place, markLayers);
    }

    // The paragraph properties in force, the most basic first: the document's defaults, the table's style, the paragraph's
    // style chain, its list level, and what it sets itself.
    private List<XElement> ParagraphLayers(IReadOnlyList<XElement> chain, XElement? level, XElement? pPr) =>
        [.. new[] { styles.DefaultParagraphProperties }.Concat(Table?.Paragraph ?? []).Concat(chain.Select(s => s.Element(W + "pPr")))
            .Append(level?.Element(W + "pPr")).Append(pPr).OfType<XElement>()];

    private List<XElement?> RunLayers(IReadOnlyList<XElement> chain) =>
        [styles.DefaultRun, .. Table?.Run ?? [], .. chain.Select(s => s.Element(W + "rPr"))];

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
        return PropertyReader.Run(layers) with { Link = Link(r) };
    }

    // A run in a link takes the link's address, when it is one a PDF may go to; the check has refused any other.
    private string? Link(XElement r) =>
        (string?)r.Ancestors(W + "hyperlink").FirstOrDefault()?.Attribute(R + "id") is { } id ? LinkAddress.Safe(links.Addresses(id)) : null;

    private void Run(XElement r, RunStyle style, FieldState field, List<Inline> inlines)
    {
        foreach (var c in r.Elements())
        {
            if (!field.Consume(c, style, inlines) && RunContent.Inline(c, style, links) is { } inline)
            {
                inlines.Add(inline);
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
