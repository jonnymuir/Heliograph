using Heliograph.Pdf;
using Heliograph.Tests.Support;

namespace Heliograph.Tests;

/// <summary>
/// Heliograph's own PDF converter against Microsoft Word itself. Fixtures/Word/inputs holds rendered letters (WordProbes);
/// beside each, Word's PDF of it, made by tools/Heliograph.WordFixtures through Word for the web. Each is compared line by
/// line, as the measuring net compares with LibreOffice, and the first differing line is a ratchet of its own.
/// </summary>
public class WordConformityTests
{
    private static readonly string Folder = Path.Combine(AppContext.BaseDirectory, "Fixtures", "Word");

    // Fixture: the first line, counting from 1, that still differs from Word. One not listed matches throughout.
    // Each fixture's first line, counting from 1, that still differs from Word; a fixture not listed matches throughout.
    // new-document is set in Aptos, Word's font since 2024, which has no free look-alike, so its lines break differently.
    // probe-calibri-exact and probe-gaps show where Word puts a font's line gap and the text in an exact or at-least line,
    // which the converter takes from LibreOffice still. The net's documents differ by a word kept on a line (twip rounding).
    private static readonly Dictionary<string, int> FirstDifference = new(StringComparer.Ordinal)
    {
        ["new-document"] = 1, ["net-1"] = 168, ["net-2"] = 2, ["net-3"] = 1, ["net-4"] = 44, ["net-lists-1"] = 60,
        ["probe-calibri-exact"] = 4, ["probe-gaps"] = 1,
    };

    public static TheoryData<string> Fixtures()
    {
        var data = new TheoryData<string>();
        foreach (var pdf in Directory.Exists(Folder) ? Directory.GetFiles(Folder, "*.word.pdf").Order(StringComparer.Ordinal).ToArray() : [])
        {
            var name = Path.GetFileName(pdf)[..^".word.pdf".Length];
            if (File.Exists(Path.Combine(Folder, "inputs", $"{name}.docx")))
            {
                data.Add(name);
            }
        }

        return data.Count == 0 ? [""] : data;
    }

    [PdfLinesTheory]
    [MemberData(nameof(Fixtures))]
    public async Task Each_fixture_is_laid_out_as_Word_lays_it_out(string name)
    {
        if (name.Length == 0)
        {
            return;
        }

        var word = PdfLines.Read(await File.ReadAllBytesAsync(Path.Combine(Folder, $"{name}.word.pdf")));
        var ours = PdfLines.Read(await new HeliographPdfConverter().ConvertAsync(await File.ReadAllBytesAsync(Path.Combine(Folder, "inputs", $"{name}.docx"))));
        var (line, difference) = PdfComparison.FirstDifference(word, ours, "Word");
        var recorded = FirstDifference.TryGetValue(name, out var known) ? known : int.MaxValue;
        line.Should().BeGreaterThanOrEqualTo(recorded, $"{name} must not differ from Word sooner than line {recorded}: {difference}");
    }

    [Fact]
    public async Task Every_input_converts()
    {
        foreach (var docx in Directory.GetFiles(Path.Combine(Folder, "inputs"), "*.docx"))
        {
            (await new HeliographPdfConverter().ConvertAsync(await File.ReadAllBytesAsync(docx))).Should().NotBeEmpty(Path.GetFileName(docx));
        }
    }

    /// <summary>Writes the fixtures' inputs, for the tool to give Word: only when HELIOGRAPH_WRITE_WORD_INPUTS names the repository's Fixtures/Word folder.</summary>
    [Fact]
    public async Task The_inputs_can_be_written_for_Word()
    {
        var target = Environment.GetEnvironmentVariable("HELIOGRAPH_WRITE_WORD_INPUTS");
        foreach (var (name, docx) in WordProbes.All())
        {
            docx.Should().NotBeEmpty(name);
            if (target is { Length: > 0 })
            {
                Directory.CreateDirectory(Path.Combine(target, "inputs"));
                await File.WriteAllBytesAsync(Path.Combine(target, "inputs", $"{name}.docx"), docx);
            }
        }
    }
}
