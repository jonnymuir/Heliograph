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
    private static readonly Dictionary<int, int> FirstDifference = new()
    {
        [1] = 128, [2] = 54, [3] = 59, [4] = 39, [5] = 17, [6] = 74, [8] = 10, [9] = 46, [10] = 24, [11] = 44, [12] = 161, [13] = 100,
        [14] = 39, [15] = 531, [16] = 48, [17] = 48, [18] = 32, [20] = 544, [21] = 45, [22] = 43, [23] = 552, [24] = 28,
        [25] = 86, [26] = 39, [28] = 27, [29] = 47, [30] = 145,
    };
    // The same, for documents made mostly of lists with levels.
    private static readonly Dictionary<int, int> FirstDifferenceInLists = new()
    {
        [1] = 61, [2] = 41, [3] = 146, [4] = 126, [5] = 38, [7] = 80, [9] = 230,
    };

    private static readonly FieldDictionary NoFields = new([]);

    [Fact]
    public void Every_random_layout_stays_within_what_the_converter_claims_to_lay_out()
    {
        for (var seed = 1; seed <= Seeds; seed++)
        {
            var compilation = DocumentTemplate.Compile(new MemoryStream(new RandomLayout(seed).Docx()), NoFields, [new HeliographPdfConverter()]);

            compilation.Diagnostics.Should().BeEmpty($"seed {seed} uses only what the converter claims");
            var lists = DocumentTemplate.Compile(new MemoryStream(new RandomLayout(seed, mostlyLists: true).Docx()), NoFields, [new HeliographPdfConverter()]);
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
        var failures = new List<string>();
        for (var seed = 1 + share; seed <= Seeds; seed += 3)
        {
            var (line, difference) = await Difference(seed);
            var recorded = FirstDifference.TryGetValue(seed, out var known) ? known : int.MaxValue;
            if (line < recorded)
            {
                failures.Add($"seed {seed} now differs from line {line}, sooner than {(recorded == int.MaxValue ? "ever" : $"line {recorded}")}: {difference}");
            }
        }

        failures.Should().BeEmpty(string.Join(Environment.NewLine, failures));
    }

    [ComparedPdfTheory]
    [InlineData(0)]
    [InlineData(1)]
    public async Task Every_random_layout_made_mostly_of_lists_is_laid_out_as_LibreOffice_lays_it_out(int share)
    {
        var failures = new List<string>();
        for (var seed = 1 + share; seed <= 10; seed += 2)
        {
            var (line, difference) = await Difference(seed, mostlyLists: true);
            var recorded = FirstDifferenceInLists.TryGetValue(seed, out var known) ? known : int.MaxValue;
            if (line < recorded)
            {
                failures.Add($"lists seed {seed} now differs from line {line}, sooner than {(recorded == int.MaxValue ? "ever" : $"line {recorded}")}: {difference}");
            }
        }

        failures.Should().BeEmpty(string.Join(Environment.NewLine, failures));
    }

    private static async Task<(int Line, string? Difference)> Difference(int seed, bool mostlyLists = false)
    {
        var compilation = DocumentTemplate.Compile(new MemoryStream(new RandomLayout(seed, mostlyLists).Docx()), NoFields);
        var docx = compilation.Document!.Render(JsonDocument.Parse("{}").RootElement, Benchmark.British).Docx!;
        var (theirs, ours) = (await new LibreOfficePdfConverter().ConvertAsync(docx), await new HeliographPdfConverter().ConvertAsync(docx));
        var (expected, actual) = (PdfLines.Read(theirs), PdfLines.Read(ours));
        if (Environment.GetEnvironmentVariable("HELIOGRAPH_NET_DUMP") is { Length: > 0 } dump)
        {
            // For looking at a failure: the letter and both PDFs, by seed.
            Directory.CreateDirectory(dump);
            await File.WriteAllBytesAsync(Path.Combine(dump, $"{(mostlyLists ? "lists-" : "")}{seed}.docx"), docx);
            await File.WriteAllBytesAsync(Path.Combine(dump, $"{(mostlyLists ? "lists-" : "")}{seed}-libreoffice.pdf"), theirs);
            await File.WriteAllBytesAsync(Path.Combine(dump, $"{(mostlyLists ? "lists-" : "")}{seed}-own.pdf"), ours);
        }

        for (var i = 0; i < Math.Max(expected.Count, actual.Count); i++)
        {
            var (e, a) = (i < expected.Count ? expected[i] : null, i < actual.Count ? actual[i] : null);
            if (e is null || a is null || e.Page != a.Page || Bullets(e.Text) != a.Text || Math.Abs(e.Top - a.Top) > 1.5)
            {
                return (i + 1, $"line {i + 1}: LibreOffice {Describe(e)}, Heliograph {Describe(a)}");
            }
        }

        return (int.MaxValue, null);
    }

    // LibreOffice keeps the private code points Symbol and Wingdings put their bullets at; Heliograph writes the
    // ordinary characters for the same shapes, which a screen reader can name. The shape is the same, so both count.
    private static string Bullets(string text) => text.Replace('\uF0B7', '\u2022').Replace('\uF0A7', '\u25AA');

    private static string Describe(PdfLine? line) =>
        line is null ? "nothing" : string.Create(CultureInfo.InvariantCulture, $"p{line.Page} at {line.Top:0.0} \"{line.Text}\"");
}
