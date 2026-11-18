using Heliograph.Pdf.Fonts;
using Heliograph.Pdf.Reading;

namespace Heliograph.Pdf.Layout;

/// <summary>
/// Where a tab takes the text, and how text after a right, centred or decimal tab lines up on its stop, with a leader of
/// dots, hyphens or a line filling the gap before it.
/// </summary>
internal static class TabStops
{
    /// <summary>
    /// The next stop after a point: the paragraph's own, then the document's default interval. A hanging indent is a
    /// stop for a list label's tab.
    /// </summary>
    public static TabStop Next(double x, ParagraphStyle s, bool hanging)
    {
        var stops = hanging && s.Indent.FirstLine < 0 ? s.Tabs.Append(new TabStop(s.Indent.Left)) : s.Tabs;
        return stops.OrderBy(t => t.Position).FirstOrDefault(t => t.Position > x + 0.01)
            ?? new TabStop((Math.Floor((x + 0.01) / s.DefaultTab) + 1) * s.DefaultTab) { Implicit = true };
    }

    /// <summary>
    /// Whether a stop is past the line's end, so its tab starts the next line: a stop of the paragraph's own beyond the end,
    /// or a default one at it or beyond.
    /// </summary>
    public static bool PastEnd(TabStop stop, double limit) => stop.Position > limit + 0.01 || (stop.Implicit && stop.Position >= limit - 0.01);

    /// <summary>
    /// Where text of this width, laid out from where the tab began, starts on its stop: ending on it, centred on it, or
    /// with its decimal point on it. It never starts before where the tab began.
    /// </summary>
    public static double Start(TabStop stop, double origin, double width, double toDecimal)
    {
        var start = stop.Align switch
        {
            "right" => stop.Position - width,
            "center" => stop.Position - (width / 2),
            "decimal" => stop.Position - toDecimal,
            _ => origin,
        };
        return Math.Max(origin, start);
    }

    /// <summary>The leader filling a gap, as text in the tab's style, or null for none.</summary>
    public static Piece? Leader(TabStop stop, double from, double to, RunStyle style)
    {
        var glyph = stop.Leader switch { "dot" => '.', "hyphen" => '-', "underscore" or "heavy" => '_', "middleDot" => '·', _ => (char?)null };
        if (glyph is not { } c || to - from <= 0)
        {
            return null;
        }

        var each = TextLook.Width(c.ToString(), style);
        var count = (int)Math.Floor((to - from) / each);
        return count <= 0 ? null : new Piece(new string(c, count), style, count * each, PieceKind.Leader);
    }
}
