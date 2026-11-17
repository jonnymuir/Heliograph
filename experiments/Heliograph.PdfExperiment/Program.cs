using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.Json;
using Heliograph;
using Heliograph.Documents;
using Heliograph.PdfExperiment;
using Heliograph.PdfExperiment.OwnEngine;
using Heliograph.Pdf.LibreOffice;

// Renders the benchmark letters, converts each one to PDF three ways, and writes the PDFs and timings to a folder.
// veraPDF and the page comparison run afterwards, from run.sh, on what this writes.

var root = FindRepo();
var output = args.Length > 0 ? args[0] : Path.Combine(root, "experiments", "out");
var fontFolder = Environment.GetEnvironmentVariable("HELIOGRAPH_FONTS") ?? "/Applications/LibreOffice.app/Contents/Resources/fonts/truetype";
Directory.CreateDirectory(output);
MigraDocRoute.UseFonts(fontFolder);

(string Letter, string Sample, string File)[] cases =
[
    ("acknowledgement", "awkward-characters", "document.docx"), ("acknowledgement", "typical", "document.word-365.docx"),
    ("decision-letter", "approved", "document.docx"), ("decision-letter", "refused", "document.docx"),
    ("annual-statement", "weekly-payer", "document.docx"), ("annual-statement", "no-contributions", "document.docx"),
];

var libreOffice = new LibreOfficePdfConverter();
var routes = new (string Name, Func<byte[], Task<byte[]>> Convert)[]
{
    ("libreoffice", docx => libreOffice.ConvertAsync(docx)),
    ("own", docx => Task.FromResult(Own(docx, fontFolder))),
    ("migradoc", docx => Task.FromResult(MigraDocRoute.Convert(Docx.Read(docx)))),
};

var results = new List<object>();
var report = new StringBuilder("| Letter | Route | Pages | Size (KB) | Median (ms) | Allocated (MB) |\n|---|---|---|---|---|---|\n");
foreach (var (letter, sample, file) in cases)
{
    var docx = Render(root, letter, sample, file);
    var folder = Path.Combine(output, file == "document.docx" ? $"{letter}-{sample}" : $"{letter}-{sample}-word365");
    Directory.CreateDirectory(folder);
    await File.WriteAllBytesAsync(Path.Combine(folder, "letter.docx"), docx);
    foreach (var (name, convert) in routes)
    {
        byte[] pdf;
        try
        {
            pdf = await convert(docx); // warm-up, and the file kept
        }
        catch (Exception e)
        {
            Console.WriteLine($"{letter}/{sample} {name}: FAILED {e.GetType().Name}: {e.Message}");
            report.Append($"| {letter}/{sample} | {name} | failed: {e.Message} | | | |\n");
            continue;
        }

        await File.WriteAllBytesAsync(Path.Combine(folder, name + ".pdf"), pdf);
        var again = name == "libreoffice" ? pdf : await convert(docx);
        Console.WriteLine($"{letter}/{sample} {name}: {(pdf.SequenceEqual(again) ? "same bytes twice" : "NOT DETERMINISTIC")}");
        var runs = name == "libreoffice" ? 3 : 20;
        var times = new List<double>();
        var allocated = GC.GetTotalAllocatedBytes(precise: true);
        for (var i = 0; i < runs; i++)
        {
            var clock = Stopwatch.StartNew();
            await convert(docx);
            times.Add(clock.Elapsed.TotalMilliseconds);
        }

        var perRun = (GC.GetTotalAllocatedBytes(precise: true) - allocated) / (double)runs / 1024 / 1024;
        var median = times.Order().ElementAt(times.Count / 2);
        var pages = System.Text.RegularExpressions.Regex.Matches(Encoding.Latin1.GetString(pdf), @"/Type\s*/Page[^s]").Count;
        results.Add(new { letter, sample, route = name, pages, bytes = pdf.Length, medianMs = median, allocatedMb = perRun });
        report.Append(CultureInfo.InvariantCulture, $"| {letter}/{sample} | {name} | {pages} | {pdf.Length / 1024.0:0} | {median:0.0} | {perRun:0.0} |\n");
        Console.WriteLine($"{letter}/{sample} {name}: {pages} pages, {pdf.Length / 1024} KB, {median:0.0} ms");
    }
}

// Sixteen letters at once, as a batch would ask for them.
report.Append("\n| Route | 16 letters at once (ms) | Peak working set of this process (MB) |\n|---|---|---|\n");
var batch = Render(root, "decision-letter", "approved");
foreach (var (name, convert) in routes)
{
    GC.Collect();
    var before = Process.GetCurrentProcess().PeakWorkingSet64;
    var clock = Stopwatch.StartNew();
    try
    {
        await Task.WhenAll(Enumerable.Range(0, 16).Select(_ => Task.Run(() => convert(batch))));
        report.Append(CultureInfo.InvariantCulture, $"| {name} | {clock.Elapsed.TotalMilliseconds:0} | {Process.GetCurrentProcess().PeakWorkingSet64 / 1024 / 1024} (was {before / 1024 / 1024}) |\n");
    }
    catch (Exception e)
    {
        report.Append($"| {name} | failed: {e.Message} | |\n");
    }
}

await File.WriteAllTextAsync(Path.Combine(output, "timings.md"), report.ToString());
await File.WriteAllTextAsync(Path.Combine(output, "timings.json"), JsonSerializer.Serialize(results, new JsonSerializerOptions { WriteIndented = true }));
Console.WriteLine(report);

static byte[] Own(byte[] docx, string fontFolder)
{
    var word = Docx.Read(docx);
    var fonts = new Fonts(fontFolder);
    var layout = new Layout(fonts);
    var pages = layout.Run(word);
    return new PdfWriter(fonts).Write(word, pages, layout.Root);
}

static byte[] Render(string root, string letter, string sample, string file = "document.docx")
{
    var folder = Path.Combine(root, "benchmarks", letter);
    using var template = File.OpenRead(Path.Combine(folder, file));
    var compilation = DocumentTemplate.Compile(template, FieldDictionary.Parse(File.ReadAllText(Path.Combine(folder, "dictionary.json"))));
    var data = JsonDocument.Parse(File.ReadAllText(Path.Combine(folder, "samples", sample + ".json"))).RootElement;
    var render = compilation.Document!.Render(data, new RenderSettings(CultureInfo.GetCultureInfo("en-GB")));
    return render.Docx ?? throw new InvalidOperationException($"{letter}/{sample} did not render.");
}

static string FindRepo()
{
    for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        if (File.Exists(Path.Combine(dir.FullName, "Heliograph.slnx"))) return dir.FullName;
    throw new InvalidOperationException("Run from inside the repository.");
}
