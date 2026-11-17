using System.Globalization;
using System.Xml.Linq;
using static Heliograph.Pdf.Reading.WordXml;

namespace Heliograph.Pdf.Reading;

/// <summary>A document's list definitions, and the running count of each list as paragraphs are read in order.</summary>
internal sealed class NumberingDefinitions
{
    private readonly Dictionary<string, XElement> _abstracts = [];
    private readonly Dictionary<string, string> _numToAbstract = [];
    private readonly Dictionary<(string Num, int Level), int> _counters = [];

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
            if (Val(n.Element(W + "abstractNumId")) is { } abstractId)
            {
                _numToAbstract[(string)n.Attribute(W + "numId")!] = abstractId;
            }
        }
    }

    /// <summary>The level a paragraph's numbering points at, or null when it is not in a list.</summary>
    public XElement? Level(XElement? numPr)
    {
        var numId = Val(numPr?.Element(W + "numId"));
        if (numId is null or "0" || !_numToAbstract.TryGetValue(numId, out var abs) || !_abstracts.TryGetValue(abs, out var definition))
        {
            return null;
        }

        var level = int.Parse(Val(numPr!.Element(W + "ilvl")) ?? "0", CultureInfo.InvariantCulture);
        return definition.Elements(W + "lvl").FirstOrDefault(l => (string?)l.Attribute(W + "ilvl") == level.ToString(CultureInfo.InvariantCulture));
    }

    /// <summary>The next label of a list at this level ("3.", or a bullet), counting on from the last; null for none.</summary>
    public string? NextLabel(XElement numPr, XElement level)
    {
        var numId = Val(numPr.Element(W + "numId"))!;
        var ilvl = int.Parse((string)level.Attribute(W + "ilvl")!, CultureInfo.InvariantCulture);
        var format = Val(level.Element(W + "numFmt")) ?? "decimal";
        if (format == "none")
        {
            return null;
        }

        var start = int.Parse(Val(level.Element(W + "start")) ?? "1", CultureInfo.InvariantCulture);
        var count = _counters.TryGetValue((numId, ilvl), out var c) ? c + 1 : start;
        _counters[(numId, ilvl)] = count;
        foreach (var deeper in _counters.Keys.Where(k => k.Num == numId && k.Level > ilvl).ToList())
        {
            _counters.Remove(deeper);
        }

        if (format == "bullet")
        {
            return "•";
        }

        var text = Val(level.Element(W + "lvlText")) ?? "%1.";
        return text.Replace($"%{ilvl + 1}", count.ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal);
    }
}
