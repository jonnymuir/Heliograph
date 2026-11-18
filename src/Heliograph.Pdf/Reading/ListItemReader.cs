using System.Xml.Linq;
using static Heliograph.Pdf.Reading.WordXml;

namespace Heliograph.Pdf.Reading;

/// <summary>Makes a paragraph a list item: its label, counted on from the items before it, its level and where the label sits.</summary>
internal sealed class ListItemReader(NumberingDefinitions numbering)
{
    private readonly ListCounters _counters = new(numbering);

    public Paragraph Listed(Paragraph paragraph, ListPlace place, List<XElement?> markLayers) => paragraph with
    {
        Label = Label(place, markLayers),
        ListLevel = place.Level,
        Placement = new LabelPlacement(
            Val(place.Definition.Element(W + "lvlJc")) switch { "right" or "end" => "right", "center" => "center", _ => "left" },
            Val(place.Definition.Element(W + "suff")) ?? "tab"),
    };

    // A label takes the paragraph mark's formatting, then its level's. A bullet set in Symbol or Wingdings, which are not
    // built in, is drawn as the same shape in the text's font. (LibreOffice on a Mac makes such a line a little taller and
    // on Linux does not; a Word fixture will settle which Word does.)
    private TextInline? Label(ListPlace place, List<XElement?> markLayers)
    {
        if (_counters.Next(place) is not { } text)
        {
            return null;
        }

        var style = PropertyReader.Run([.. markLayers, place.Definition.Element(W + "rPr")]);
        if (Val(place.Definition.Element(W + "numFmt")) != "bullet")
        {
            return new TextInline(text, style);
        }

        var (bullet, ownFont) = ListLabels.Bullet(text, style.Font);
        return new TextInline(bullet, ownFont ? style : style with { Font = PropertyReader.Run(markLayers).Font });
    }
}
