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
    // LibreOffice versions differ a little, so each number is the lower of the CI runner's (Ubuntu) and a Mac's.
    private static readonly Dictionary<int, int> FirstDifference = new()
    {
        [1] = 128, [2] = 54, [3] = 59, [4] = 39, [5] = 17, [6] = 74, [8] = 10, [9] = 46, [10] = 24, [11] = 44, [12] = 161, [13] = 97,
        [14] = 39, [15] = 531, [16] = 48, [17] = 48, [18] = 32, [19] = 1022, [20] = 539, [21] = 186, [22] = 43, [23] = 41, [24] = 28,
        [25] = 86, [26] = 39, [27] = 37, [28] = 27, [29] = 47, [30] = 145,
    };
    private static readonly FieldDictionary NoFields = new([]);

    [Fact]
    public void Every_random_layout_stays_within_what_the_converter_claims_to_lay_out()
    {
        for (var seed = 1; seed <= Seeds; seed++)
        {
            var compilation = DocumentTemplate.Compile(new MemoryStream(new RandomLayout(seed).Docx()), NoFields, [new HeliographPdfConverter()]);

            compilation.Diagnostics.Should().BeEmpty($"seed {seed} uses only what the converter claims");
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

    private static async Task<(int Line, string? Difference)> Difference(int seed)
    {
        var compilation = DocumentTemplate.Compile(new MemoryStream(new RandomLayout(seed).Docx()), NoFields);
        var docx = compilation.Document!.Render(JsonDocument.Parse("{}").RootElement, Benchmark.British).Docx!;
        var (theirs, ours) = (await new LibreOfficePdfConverter().ConvertAsync(docx), await new HeliographPdfConverter().ConvertAsync(docx));
        var (expected, actual) = (PdfLines.Read(theirs), PdfLines.Read(ours));
        if (Environment.GetEnvironmentVariable("HELIOGRAPH_NET_DUMP") is { Length: > 0 } dump)
        {
            // For looking at a failure: the letter and both PDFs, by seed.
            Directory.CreateDirectory(dump);
            await File.WriteAllBytesAsync(Path.Combine(dump, $"{seed}.docx"), docx);
            await File.WriteAllBytesAsync(Path.Combine(dump, $"{seed}-libreoffice.pdf"), theirs);
            await File.WriteAllBytesAsync(Path.Combine(dump, $"{seed}-own.pdf"), ours);
        }

        for (var i = 0; i < Math.Max(expected.Count, actual.Count); i++)
        {
            var (e, a) = (i < expected.Count ? expected[i] : null, i < actual.Count ? actual[i] : null);
            if (e is null || a is null || e.Page != a.Page || e.Text != a.Text || Math.Abs(e.Top - a.Top) > 1.5)
            {
                return (i + 1, $"line {i + 1}: LibreOffice {Describe(e)}, Heliograph {Describe(a)}");
            }
        }

        return (int.MaxValue, null);
    }

    private static string Describe(PdfLine? line) =>
        line is null ? "nothing" : string.Create(CultureInfo.InvariantCulture, $"p{line.Page} at {line.Top:0.0} \"{line.Text}\"");
}
