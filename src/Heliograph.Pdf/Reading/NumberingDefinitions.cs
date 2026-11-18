using System.Globalization;
using System.Xml.Linq;
using static Heliograph.Pdf.Reading.WordXml;

namespace Heliograph.Pdf.Reading;

/// <summary>A paragraph's place in a list: the level it is at and that level's definition.</summary>
internal sealed record ListPlace(string NumId, string AbstractId, int Level, XElement Definition);

/// <summary>
/// A document's list definitions, and the running count of each list as paragraphs are read in order, as Word counts
/// them: lists that share a definition count on together unless one sets where it starts (Restart numbering, Set
/// numbering value); a level starts again after a higher level, unless it says otherwise.
/// </summary>
internal sealed class NumberingDefinitions
{
    private readonly Dictionary<string, XElement> _abstracts = [];
    private readonly Dictionary<string, XElement> _nums = [];
    private readonly Dictionary<(string Abstract, int Level), int> _counters = [];
    private readonly HashSet<string> _started = [];

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

    /// <summary>The next label at this place ("3.", "1.2.", "iv)" or a bullet), counting on; null for none.</summary>
    public string? NextLabel(ListPlace place)
    {
        StartOverrides(place);
        var key = (place.AbstractId, place.Level);
        _counters[key] = _counters.TryGetValue(key, out var count) ? count + 1 : Start(place.Definition);
        RestartDeeper(place);

        var format = Val(place.Definition.Element(W + "numFmt")) ?? "decimal";
        var text = Val(place.Definition.Element(W + "lvlText")) ?? (format == "bullet" ? "•" : $"%{place.Level + 1}.");
        if (format == "bullet")
        {
            return text;
        }

        var legal = On(place.Definition.Element(W + "isLgl"));
        for (var n = 1; n <= 9; n++)
        {
            if (text.Contains($"%{n}", StringComparison.Ordinal))
            {
                text = text.Replace($"%{n}", Shown(place, n - 1, legal), StringComparison.Ordinal);
            }
        }

        return text.Length == 0 ? null : text;
    }

    private static int Level(XElement e) => int.TryParse((string?)e.Attribute(W + "ilvl"), CultureInfo.InvariantCulture, out var l) ? l : 0;

    private static int Start(XElement? definition) =>
        int.TryParse(Val(definition?.Element(W + "start")), CultureInfo.InvariantCulture, out var s) ? s : 1;

    // A level's number as it appears in a label: its count, in its own style, or as a plain number in a legal label.
    private string Shown(ListPlace place, int level, bool legal)
    {
        var definition = Definition(place.NumId, place.AbstractId, level);
        var count = _counters.TryGetValue((place.AbstractId, level), out var c) ? c : Start(definition);
        var format = Val(definition?.Element(W + "numFmt")) ?? "decimal";
        return ListLabels.Number(count, legal && format != "none" ? "decimal" : format);
    }

    // The first paragraph of a list that sets where a level starts begins that level again there.
    private void StartOverrides(ListPlace place)
    {
        if (!_started.Add(place.NumId))
        {
            return;
        }

        foreach (var o in _nums[place.NumId].Elements(W + "lvlOverride"))
        {
            if (int.TryParse(Val(o.Element(W + "startOverride")), CultureInfo.InvariantCulture, out var start))
            {
                _counters[(place.AbstractId, Level(o))] = start - 1;
            }
        }
    }

    // A deeper level starts again after this one, unless its lvlRestart names a higher level only, or 0 for never.
    private void RestartDeeper(ListPlace place)
    {
        foreach (var key in _counters.Keys.Where(k => k.Abstract == place.AbstractId && k.Level > place.Level).ToList())
        {
            var restart = Val(Definition(place.NumId, place.AbstractId, key.Level)?.Element(W + "lvlRestart"));
            var after = int.TryParse(restart, CultureInfo.InvariantCulture, out var r) ? r : key.Level;
            if (place.Level < after)
            {
                _counters.Remove(key);
            }
        }
    }
}
