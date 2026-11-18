using Heliograph.Pdf.Reading;

namespace Heliograph.Pdf.Layout;

/// <summary>A line's height under each of Word's line-spacing rules, and where its text sits in it.</summary>
internal static class LineHeights
{
    /// <summary>
    /// The line's height: exact, at least the height set, or its natural height with line spacing over or under single
    /// adding or taking away a share of the text's height (not a picture's).
    /// </summary>
    public static double Height(Spacing spacing, double natural, double text) => spacing.LineRule switch
    {
        "exact" => spacing.Line / 20,
        "atLeast" => Math.Max(spacing.Line / 20, natural),
        _ => natural + (text * ((spacing.Line / 240) - 1)),
    };

    /// <summary>The extra under a line set wider than single, which may hang below the foot of a page.</summary>
    public static double Hang(Spacing spacing, double height, double natural) =>
        spacing.LineRule is "exact" or "atLeast" ? 0 : Math.Max(0, height - natural);

    /// <summary>
    /// How much further down the text sits in a line of exact or least height, the difference being above it. In an
    /// exact line the text stands on its descent at the line's foot, with no gap under it; in an at-least line it keeps
    /// its gap.
    /// </summary>
    public static double Lift(Spacing spacing, double height, double natural, double gap) => spacing.LineRule switch
    {
        "exact" => height - natural + gap,
        "atLeast" => height - natural,
        _ => 0,
    };
}
