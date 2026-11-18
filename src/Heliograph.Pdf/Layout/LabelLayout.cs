using Heliograph.Pdf.Fonts;
using Heliograph.Pdf.Reading;

namespace Heliograph.Pdf.Layout;

/// <summary>Where a list label goes on its line, as its placement says.</summary>
internal static class LabelLayout
{
    /// <summary>Where the label starts: at the first line's start, or ending or centred there.</summary>
    public static double Start(LabelPlacement placement, double x, double width) => placement.Align switch
    {
        "right" => x - width,
        "center" => x - (width / 2),
        _ => x,
    };

    /// <summary>The gap after a label followed by a space or by nothing.</summary>
    public static double Gap(LabelPlacement placement, RunStyle style) =>
        placement.Suffix == "space" ? TextLook.Width(" ", style) : 0;
}
