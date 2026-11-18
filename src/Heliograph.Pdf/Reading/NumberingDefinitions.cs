using System.Globalization;
using System.Xml.Linq;
using static Heliograph.Pdf.Reading.WordXml;

namespace Heliograph.Pdf.Reading;

/// <summary>A paragraph's place in a list: the level it is at and that level's definition.</summary>
internal sealed record ListPlace(string NumId, string AbstractId, int Level, XElement Definition);

/// <summary>A document's list definitions: which list and level a paragraph is in, and how each level is defined.</summary>
internal sealed class NumberingDefinitions
{
    private readonly Dictionary<string, XElement> _abstracts = [];
    private readonly Dictionary<string, XElement> _nums = [];

    public NumberingDefinitions(XDocument? numbering)
    {
        if (numbering?.Root is not { } root)
        {
            return;
        }

        foreach (var a in root.Elements(W + "abstractNum"))
        {
            _abstracts[(string)a.Attribute(W + "abstractNumId")!] = a;
        }

        foreach (var n in root.Elements(W + "num"))
        {
            _nums[(string)n.Attribute(W + "numId")!] = n;
        }
    }

    /// <summary>Where a paragraph's numbering points, or null when it is not in a list.</summary>
    public ListPlace? Place(XElement? numPr)
    {
        var numId = Val(numPr?.Element(W + "numId"));
        if (AbstractOf(numId) is not { } abstractId)
        {
            return null;
        }

        var level = int.TryParse(Val(numPr!.Element(W + "ilvl")), CultureInfo.InvariantCulture, out var l) ? l : 0;
        return Definition(numId!, abstractId, level) is { } definition ? new ListPlace(numId!, abstractId, level, definition) : null;
    }

    // The shared definition a list points to, when both exist; numId 0 means no list.
    private string? AbstractOf(string? numId)
    {
        var abstractId = numId is null or "0" ? null : Val(_nums.GetValueOrDefault(numId)?.Element(W + "abstractNumId"));
        return abstractId is not null && _abstracts.ContainsKey(abstractId) ? abstractId : null;
    }

    /// <summary>The definition of a level of a list: the list's own override of it, else its shared definition.</summary>
    public XElement? Definition(string numId, string abstractId, int level)
    {
        var own = _nums[numId].Elements(W + "lvlOverride").FirstOrDefault(o => Level(o) == level)?.Element(W + "lvl");
        return own ?? _abstracts[abstractId].Elements(W + "lvl").FirstOrDefault(e => Level(e) == level);
    }

    /// <summary>The levels a list sets for itself: Restart numbering and Set numbering value.</summary>
    public IEnumerable<XElement> Overrides(string numId) => _nums[numId].Elements(W + "lvlOverride");

    public static int Level(XElement e) => int.TryParse((string?)e.Attribute(W + "ilvl"), CultureInfo.InvariantCulture, out var l) ? l : 0;

    public static int Start(XElement? definition) =>
        int.TryParse(Val(definition?.Element(W + "start")), CultureInfo.InvariantCulture, out var s) ? s : 1;
}
