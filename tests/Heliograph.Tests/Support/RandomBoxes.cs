namespace Heliograph.Tests.Support;

/// <summary>The borders and shading RandomLayout puts round some paragraphs: a box, a box with a line between, shading.</summary>
internal static class RandomBoxes
{
    private static readonly string[] Frames =
    [
        "<w:pBdr><w:top w:val=\"single\" w:sz=\"12\" w:space=\"4\" w:color=\"auto\"/><w:left w:val=\"single\" w:sz=\"12\" w:space=\"4\" w:color=\"auto\"/>"
            + "<w:bottom w:val=\"single\" w:sz=\"12\" w:space=\"4\" w:color=\"auto\"/><w:right w:val=\"single\" w:sz=\"12\" w:space=\"4\" w:color=\"auto\"/></w:pBdr>",
        "<w:pBdr><w:top w:val=\"single\" w:sz=\"4\" w:space=\"1\" w:color=\"auto\"/><w:bottom w:val=\"single\" w:sz=\"4\" w:space=\"1\" w:color=\"auto\"/>"
            + "<w:between w:val=\"single\" w:sz=\"4\" w:space=\"1\" w:color=\"auto\"/></w:pBdr>",
        "<w:shd w:val=\"clear\" w:color=\"auto\" w:fill=\"FFF2CC\"/>",
    ];

    /// <summary>A paragraph's properties with a frame put in, for about one paragraph in ten.</summary>
    public static string Into(string properties, Random random)
    {
        if (random.Next(10) != 0)
        {
            return properties;
        }

        var frame = Frames[random.Next(Frames.Length)];
        // pBdr comes after spacing-free keeps and before spacing; shd before tabs' successors: both go before w:spacing.
        var at = properties.IndexOf("<w:spacing", StringComparison.Ordinal);
        return at < 0 ? properties + frame : properties.Insert(at, frame);
    }
}
