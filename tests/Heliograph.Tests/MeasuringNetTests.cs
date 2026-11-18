using System.Globalization;
using System.Text.Json;
using Heliograph.Documents;
using Heliograph.Pdf;
using Heliograph.Pdf.LibreOffice;
using Heliograph.Tests.Support;

namespace Heliograph.Tests;

/// <summary>
/// The measuring net for Heliograph's own PDF converter (docs/pdf-engine.md, stage 2): documents built at random from
/// everything it claims to lay out, each converted by it and by LibreOffice and compared line for line. A failure names
/// the seed; <c>new RandomLayout(seed)</c> builds that document again. HELIOGRAPH_NET_SEEDS sets how many are tried, and
/// HELIOGRAPH_NET_DUMP a folder to write each letter and both PDFs to.
/// </summary>
/// <remarks>
/// It is a ratchet. Most random documents still differ from LibreOffice somewhere, so each seed's first differing line is
/// recorded, and the test fails if a change makes any seed differ sooner. A fix moves a seed's number up, or takes it
/// off the list when the whole document matches; the numbers only ever go up.
/// </remarks>
public class MeasuringNetTests
{
    private static readonly int Seeds = int.TryParse(Environment.GetEnvironmentVariable("HELIOGRAPH_NET_SEEDS"), CultureInfo.InvariantCulture, out var n) ? n : 30;

    // Seed: the first line, counting from 1, that still differs from LibreOffice. A seed not listed matches throughout.
    // LibreOffice versions differ a little, so each number is the lower of the CI runner's (Ubuntu) and a Mac's. Odd seeds
    // became new-Word-document spacing on 18 November 2026, so seed 21's document changed and its number was set again.
    // Seed 25's is the Mac's: there LibreOffice makes a line with a Symbol bullet a little taller, and on Linux it does not.
    // Paragraphs gained pictures on 18 November 2026, so seed 12's document changed and its number was set again. A third
    // of documents became Calibri or Cambria the same day, so seeds 3, 13, 15 and 30 (and lists seed 1) were set again too.
    // LibreOffice on Linux sets text in an exact-height Calibri line higher than on a Mac, so those seeds take Linux's.
    // Runs gained formatting the same day, so seeds 7, 9, 13 and 19 (and lists seed 10) were set again once more; and
    // paragraphs began leaving out space between paragraphs of one style, so lists seed 4 was set again. Tables gained
    // styles, row heights, alignment and text at the middle or foot of a cell, so seeds with tables were set again, and
    // again when they gained cells merged down a column and cell shading.
    private static readonly Dictionary<int, int> FirstDifference = new()
    {
        [1] = 241, [2] = 66, [3] = 22, [4] = 165, [5] = 25, [6] = 87, [7] = 3, [8] = 10, [9] = 61, [10] = 24, [11] = 44,
        [12] = 92, [13] = 3, [14] = 39, [15] = 56, [16] = 48, [17] = 38, [18] = 13, [19] = 89, [20] = 201, [21] = 45,
        [22] = 54, [23] = 99, [24] = 28, [25] = 33, [26] = 17, [27] = 189, [28] = 27, [29] = 52, [30] = 61,
    };
    // The same, for documents made mostly of lists with levels.
    private static readonly Dictionary<int, int> FirstDifferenceInLists = new()
    {
        [1] = 148, [2] = 15, [3] = 24, [4] = 43, [5] = 29, [6] = 62, [7] = 3, [8] = 88, [9] = 90, [10] = 122,
    };

    private static readonly FieldDictionary NoFields = new([]);

    [Fact]
    public void Every_random_layout_stays_within_what_the_converter_claims_to_lay_out()
    {
        for (var seed = 1; seed <= Seeds; seed++)
        {
            var compilation = DocumentTemplate.Compile(new MemoryStream(new RandomLayout(seed).Docx()), NoFields, [new HeliographPdfConverter()]);

            compilation.Diagnostics.Should().BeEmpty($"seed {seed} uses only what the converter claims");
            var lists = DocumentTemplate.Compile(new MemoryStream(new RandomLayout(seed, LayoutMix.MostlyLists).Docx()), NoFields, [new HeliographPdfConverter()]);
            lists.Diagnostics.Should().BeEmpty($"seed {seed}, mostly lists, uses only what the converter claims");
        }
    }

    [ComparedPdfTheory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public async Task Every_random_layout_is_laid_out_as_LibreOffice_lays_it_out(int share)
    {
        // The seeds are shared between three tests so they run side by side.
        var failures = await Failures(Enumerable.Range(0, Seeds).Where(n => n % 3 == share).Select(n => n + 1), LayoutMix.Everything, FirstDifference);
        failures.Should().BeEmpty(string.Join(Environment.NewLine, failures));
    }

    [ComparedPdfTheory]
    [InlineData(0)]
    [InlineData(1)]
    public async Task Every_random_layout_made_mostly_of_lists_is_laid_out_as_LibreOffice_lays_it_out(int share)
    {
        var failures = await Failures(Enumerable.Range(0, 10).Where(n => n % 2 == share).Select(n => n + 1), LayoutMix.MostlyLists, FirstDifferenceInLists);
        failures.Should().BeEmpty(string.Join(Environment.NewLine, failures));
    }

    // Each seed that now differs sooner than its recorded line.
    private static async Task<List<string>> Failures(IEnumerable<int> seeds, LayoutMix mix, Dictionary<int, int> recorded)
    {
        var failures = new List<string>();
        foreach (var seed in seeds)
        {
            var (line, difference) = await Difference(seed, mix);
            var known = recorded.TryGetValue(seed, out var first) ? first : int.MaxValue;
            if (line < known)
            {
                var name = mix == LayoutMix.MostlyLists ? "lists seed" : "seed";
                failures.Add($"{name} {seed} now differs from line {line}, sooner than {(known == int.MaxValue ? "ever" : $"line {known}")}: {difference}");
            }
        }

        return failures;
    }

    private static async Task<(int Line, string? Difference)> Difference(int seed, LayoutMix mix = LayoutMix.Everything)
    {
        var compilation = DocumentTemplate.Compile(new MemoryStream(new RandomLayout(seed, mix).Docx()), NoFields);
        var docx = compilation.Document!.Render(JsonDocument.Parse("{}").RootElement, Benchmark.British).Docx!;
        var (theirs, ours) = (await new LibreOfficePdfConverter().ConvertAsync(docx), await new HeliographPdfConverter().ConvertAsync(docx));
        var (expected, actual) = (PdfLines.Read(theirs), PdfLines.Read(ours));
        if (Environment.GetEnvironmentVariable("HELIOGRAPH_NET_DUMP") is { Length: > 0 } dump)
        {
            // For looking at a failure: the letter and both PDFs, by seed.
            Directory.CreateDirectory(dump);
            var name = Path.Combine(dump, $"{mix}-{seed}");
            await File.WriteAllBytesAsync($"{name}.docx", docx);
            await File.WriteAllBytesAsync($"{name}-libreoffice.pdf", theirs);
            await File.WriteAllBytesAsync($"{name}-own.pdf", ours);
        }

        for (var i = 0; i < Math.Max(expected.Count, actual.Count); i++)
        {
            var (e, a) = (i < expected.Count ? expected[i] : null, i < actual.Count ? actual[i] : null);
            if (e is null || a is null || e.Page != a.Page || Letters(Bullets(e.Text)) != Letters(a.Text) || Math.Abs(e.Top - a.Top) > 1.5)
            {
                return (i + 1, $"line {i + 1}: LibreOffice {Describe(e)}, Heliograph {Describe(a)}");
            }
        }

        return (int.MaxValue, null);
    }

    // LibreOffice keeps the private code points Symbol and Wingdings put their bullets at; Heliograph writes the
    // ordinary characters for the same shapes, which a screen reader can name. The shape is the same, so both count.
    private static string Bullets(string text) => text.Replace('\uF0B7', '\u2022').Replace('\uF0A7', '\u25AA');

    // A line's letters without its spaces: LibreOffice places each letter of spaced-out text on its own, so Poppler reads
    // them as words, where Heliograph draws the word with character spacing. Which words are on which line still counts.
    // A leader is left out too: each converter draws as many dots as fit.
    private static string Letters(string text) =>
        System.Text.RegularExpressions.Regex.Replace(text, "[._\\-\u00B7]{3,}", "").Replace(" ", "", StringComparison.Ordinal);

    private static string Describe(PdfLine? line) =>
        line is null ? "nothing" : string.Create(CultureInfo.InvariantCulture, $"p{line.Page} at {line.Top:0.0} \"{line.Text}\"");
}
