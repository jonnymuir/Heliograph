using System.Xml.Linq;
using static Heliograph.Pdf.Reading.WordXml;

namespace Heliograph.Pdf.Reading;

/// <summary>A paragraph's tab stops through its style chain: each set adds stops, or clears one at the same place.</summary>
internal static class TabStopReader
{
    public static List<TabStop> Read(IReadOnlyList<XElement> layers)
    {
        var tabs = new List<TabStop>();
        foreach (var tab in layers.SelectMany(x => x.Element(W + "tabs")?.Elements(W + "tab") ?? []))
        {
            var position = Twips(tab, "pos") ?? 0;
            tabs.RemoveAll(t => Math.Abs(t.Position - position) < 0.01);
            if (Val(tab) is not ("clear" or "num"))
            {
                tabs.Add(new TabStop(position, Align(Val(tab)), (string?)tab.Attribute(W + "leader") ?? "none"));
            }
        }

        return [.. tabs.OrderBy(t => t.Position)];
    }

    private static string Align(string? value) => value switch
    {
        "right" or "end" => "right",
        "center" => "center",
        "decimal" => "decimal",
        _ => "left",
    };
}
