using System.Xml.Linq;
using static Heliograph.Pdf.Reading.WordXml;

namespace Heliograph.Pdf.Reading;

/// <summary>A document's styles: each style found by id with the styles it is based on, and the document defaults.</summary>
internal sealed class StyleSheet
{
    private readonly Dictionary<string, XElement> _byId = [];

    public StyleSheet(XDocument? styles)
    {
        if (styles?.Root is not { } root)
        {
            return;
        }

        foreach (var style in root.Elements(W + "style"))
        {
            if ((string?)style.Attribute(W + "styleId") is { } id)
            {
                _byId[id] = style;
            }
        }

        DefaultParagraph = DefaultParagraphStyle(root);
        DefaultTable = root.Elements(W + "style")
            .FirstOrDefault(s => (string?)s.Attribute(W + "type") == "table" && (string?)s.Attribute(W + "default") is "1" or "true")
            ?.Attribute(W + "styleId")?.Value;
        var defaults = root.Element(W + "docDefaults");
        DefaultRun = defaults?.Element(W + "rPrDefault")?.Element(W + "rPr");
        DefaultParagraphProperties = defaults?.Element(W + "pPrDefault")?.Element(W + "pPr");
        Language = (string?)DefaultRun?.Element(W + "lang")?.Attribute(W + "val") ?? "en-GB";
    }

    public string DefaultParagraph { get; } = "Normal";

    /// <summary>The style a table takes when it names none (Word's Table Normal), if the document has one.</summary>
    public string? DefaultTable { get; }

    public XElement? DefaultRun { get; }

    public XElement? DefaultParagraphProperties { get; }

    public string Language { get; } = "en-GB";

    private static string DefaultParagraphStyle(XElement root) =>
        root.Elements(W + "style")
            .FirstOrDefault(s => (string?)s.Attribute(W + "type") == "paragraph" && (string?)s.Attribute(W + "default") is "1" or "true")
            ?.Attribute(W + "styleId")?.Value ?? "Normal";

    /// <summary>A style and everything it is based on, the most basic first.</summary>
    public IReadOnlyList<XElement> Chain(string id)
    {
        var chain = new List<XElement>();
        var current = id;
        while (current is not null && _byId.TryGetValue(current, out var style) && chain.Count < 32)
        {
            chain.Insert(0, style);
            current = Val(style.Element(W + "basedOn"));
        }

        return chain;
    }
}
