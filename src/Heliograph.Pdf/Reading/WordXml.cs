using System.Globalization;
using System.Xml.Linq;

namespace Heliograph.Pdf.Reading;

/// <summary>The WordprocessingML names and the few conversions every reader needs.</summary>
internal static class WordXml
{
    public static readonly XNamespace W = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";
    public static readonly XNamespace R = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
    public static readonly XNamespace Dc = "http://purl.org/dc/elements/1.1/";

    /// <summary>Whether an on-off property is on: present with no value, or with true, 1 or on.</summary>
    public static bool On(XElement? e) => e is not null && (string?)e.Attribute(W + "val") is null or "true" or "1" or "on";

    public static string? Val(XElement? e) => (string?)e?.Attribute(W + "val");

    /// <summary>An attribute in twentieths of a point, as points.</summary>
    public static double? Twips(XElement? e, string attribute) =>
        e?.Attribute(W + attribute) is { } a && double.TryParse(a.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? v / 20 : null;

    public static double Number(string value) => double.Parse(value, NumberStyles.Float, CultureInfo.InvariantCulture);
}
