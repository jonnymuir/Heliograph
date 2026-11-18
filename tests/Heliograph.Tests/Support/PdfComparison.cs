using System.Globalization;
using System.Text.RegularExpressions;

namespace Heliograph.Tests.Support;

/// <summary>
/// The measure the measuring net and the Word fixtures share: line by line, the same words on the same page, each within
/// a point and a half of where the reference has it. Returns the first line, counting from 1, that differs.
/// </summary>
/// <remarks>
/// Gap is the share of a line's font size that the reference puts below its text where Heliograph, as Word does, puts it
/// above: LibreOffice with Calibri, whose line gap is 0.22 of its size. A line may then also sit that much lower than the
/// reference has it (or where the reference has it, in an exact line, where both place text alike).
/// </remarks>
internal static partial class PdfComparison
{
    /// <summary>Calibri's line gap as a share of its size: 452 of 2048 units.</summary>
    public const double CalibriGap = 452.0 / 2048;

    public static (int Line, string? Difference) FirstDifference(IReadOnlyList<PdfLine> expected, IReadOnlyList<PdfLine> actual, string reference, double gap = 0)
    {
        for (var i = 0; i < Math.Max(expected.Count, actual.Count); i++)
        {
            var (e, a) = (i < expected.Count ? expected[i] : null, i < actual.Count ? actual[i] : null);
            if (e is null || a is null || e.Page != a.Page || Letters(Bullets(e.Text)) != Letters(Bullets(a.Text)) || !Near(e, a, gap))
            {
                return (i + 1, $"line {i + 1}: {reference} {Describe(e)}, Heliograph {Describe(a)}");
            }
        }

        return (int.MaxValue, null);
    }

    private static bool Near(PdfLine e, PdfLine a, double gap) =>
        Math.Abs(e.Top - a.Top) <= 1.5 || Math.Abs(e.Top + (gap * a.Height) - a.Top) <= 1.5;

    // LibreOffice and Word keep the private code points Symbol and Wingdings put their bullets at; Heliograph writes the
    // ordinary characters for the same shapes, which a screen reader can name. The shape is the same, so both count.
    private static string Bullets(string text) => text.Replace('', '•').Replace('', '▪');

    // A line's letters without its spaces: LibreOffice places each letter of spaced-out text on its own, so Poppler reads
    // them as words, where Heliograph draws the word with character spacing. Which words are on which line still counts.
    // A leader is left out too: each converter draws as many dots as fit.
    private static string Letters(string text) => Leader().Replace(text, "").Replace(" ", "", StringComparison.Ordinal);

    [GeneratedRegex("[._\\-·]{3,}")]
    private static partial Regex Leader();

    private static string Describe(PdfLine? line) =>
        line is null ? "nothing" : string.Create(CultureInfo.InvariantCulture, $"p{line.Page} at {line.Top:0.0} \"{line.Text}\"");
}
