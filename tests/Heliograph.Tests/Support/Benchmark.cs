using System.Globalization;
using System.Text.Json;
using Heliograph.Documents;

namespace Heliograph.Tests.Support;

/// <summary>Loads a benchmark letter from the copy of <c>benchmarks/</c> beside the test assembly.</summary>
internal static class Benchmark
{
    public static readonly RenderSettings British = new(CultureInfo.GetCultureInfo("en-GB"));

    public static string PathTo(string letter, string file) =>
        Path.Combine(AppContext.BaseDirectory, "benchmarks", letter, file);

    public static DataDictionary Dictionary(string letter) =>
        DataDictionary.Parse(File.ReadAllText(PathTo(letter, "dictionary.json")));

    public static JsonElement Sample(string letter, string sample) =>
        JsonDocument.Parse(File.ReadAllText(PathTo(letter, Path.Combine("samples", sample + ".json")))).RootElement;

    public static DocumentCompilation Compile(string letter, string file = "document.docx")
    {
        using var docx = File.OpenRead(PathTo(letter, file));
        return DocumentTemplate.Compile(docx, Dictionary(letter));
    }

    public static JsonElement Data(string json) => JsonDocument.Parse(json).RootElement;
}
