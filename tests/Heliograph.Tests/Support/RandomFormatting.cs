using System.Text;

namespace Heliograph.Tests.Support;

/// <summary>
/// The run formatting RandomLayout sets on some runs of a paragraph: everything the converter claims to draw beyond bold,
/// italic and underline.
/// </summary>
internal static class RandomFormatting
{
    private static readonly string[] Choices =
    [
        "<w:color w:val=\"C00000\"/>",
        "<w:color w:val=\"1F4E79\" w:themeColor=\"accent1\" w:themeShade=\"80\"/>",
        "<w:highlight w:val=\"yellow\"/>",
        "<w:shd w:val=\"clear\" w:color=\"auto\" w:fill=\"E2EFDA\"/>",
        "<w:strike/>",
        "<w:dstrike/>",
        "<w:vertAlign w:val=\"superscript\"/>",
        "<w:vertAlign w:val=\"subscript\"/>",
        "<w:caps/>",
        "<w:spacing w:val=\"30\"/>",
        "<w:spacing w:val=\"-8\"/>",
    ];

    /// <summary>The paragraph with about one run in six formatted.</summary>
    public static string Into(string paragraph, Random random)
    {
        var result = new StringBuilder();
        var parts = paragraph.Split("<w:r><w:t");
        result.Append(parts[0]);
        foreach (var part in parts.Skip(1))
        {
            result.Append(random.Next(6) == 0 ? $"<w:r><w:rPr>{Choices[random.Next(Choices.Length)]}</w:rPr><w:t" : "<w:r><w:t").Append(part);
        }

        return result.ToString();
    }
}
