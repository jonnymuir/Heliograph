using System.Globalization;
using static Heliograph.Pdf.Reading.WordXml;

namespace Heliograph.Pdf.Reading;

/// <summary>
/// The running count of each list as paragraphs are read in order, as Word counts them: lists that share a definition
/// count on together unless one sets where it starts (Restart numbering, Set numbering value); a level starts again after
/// a higher level, unless it says otherwise.
/// </summary>
internal sealed class ListCounters(NumberingDefinitions definitions)
{
    private readonly Dictionary<(string Abstract, int Level), int> _counts = [];
    private readonly HashSet<string> _started = [];

    /// <summary>The next label at this place ("3.", "1.2.", "iv)" or a bullet), counting on; null for none.</summary>
    public string? Next(ListPlace place)
    {
        StartOverrides(place);
        var key = (place.AbstractId, place.Level);
        _counts[key] = _counts.TryGetValue(key, out var count) ? count + 1 : NumberingDefinitions.Start(place.Definition);
        RestartDeeper(place);

        var format = Val(place.Definition.Element(W + "numFmt")) ?? "decimal";
        var text = Val(place.Definition.Element(W + "lvlText")) ?? (format == "bullet" ? "•" : $"%{place.Level + 1}.");
        return format == "bullet" ? text : Filled(place, text);
    }

    // Each %n in a label's text as level n's number.
    private string? Filled(ListPlace place, string text)
    {
        var legal = On(place.Definition.Element(W + "isLgl"));
        for (var n = 1; n <= 9; n++)
        {
            text = text.Replace($"%{n}", Shown(place, n - 1, legal), StringComparison.Ordinal);
        }

        return text.Length == 0 ? null : text;
    }

    // A level's number as it appears in a label: its count, in its own style, or as a plain number in a legal label.
    private string Shown(ListPlace place, int level, bool legal)
    {
        var definition = definitions.Definition(place.NumId, place.AbstractId, level);
        var count = _counts.TryGetValue((place.AbstractId, level), out var c) ? c : NumberingDefinitions.Start(definition);
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

        foreach (var o in definitions.Overrides(place.NumId))
        {
            if (int.TryParse(Val(o.Element(W + "startOverride")), CultureInfo.InvariantCulture, out var start))
            {
                _counts[(place.AbstractId, NumberingDefinitions.Level(o))] = start - 1;
            }
        }
    }

    // A deeper level starts again after this one, unless its lvlRestart names a higher level only, or 0 for never.
    private void RestartDeeper(ListPlace place)
    {
        foreach (var key in _counts.Keys.Where(k => k.Abstract == place.AbstractId && k.Level > place.Level).ToList())
        {
            var restart = Val(definitions.Definition(place.NumId, place.AbstractId, key.Level)?.Element(W + "lvlRestart"));
            var after = int.TryParse(restart, CultureInfo.InvariantCulture, out var r) ? r : key.Level;
            if (place.Level < after)
            {
                _counts.Remove(key);
            }
        }
    }
}
