using Heliograph.Library;

namespace Heliograph.ReferenceApp;

/// <summary>
/// Demo only. On first start, puts the three benchmark letters into the app's folder, each with its dictionary and
/// sample recipients, and publishes them, so there is something to try at once. Delete the folder to start again.
/// </summary>
internal static class ReferenceSeed
{
    private static readonly (string Key, string Name)[] Letters =
    [
        ("acknowledgement", "Acknowledgement of an application"),
        ("decision-letter", "Decision on an application"),
        ("annual-statement", "Annual contributions statement"),
    ];

    public static async Task RunAsync(IServiceProvider services, string folder)
    {
        var dictionaries = Path.Combine(folder, "dictionaries");
        if (Directory.Exists(dictionaries))
        {
            return;
        }

        var library = services.GetRequiredService<CorrespondenceLibrary>();
        var benchmarks = Path.Combine(AppContext.BaseDirectory, "benchmarks");
        foreach (var (key, name) in Letters)
        {
            var from = Path.Combine(benchmarks, key);
            Directory.CreateDirectory(Path.Combine(dictionaries, key + ".samples"));
            File.Copy(Path.Combine(from, "dictionary.json"), Path.Combine(dictionaries, key + ".json"));
            foreach (var sample in Directory.GetFiles(Path.Combine(from, "samples"), "*.json"))
            {
                File.Copy(sample, Path.Combine(dictionaries, key + ".samples", Path.GetFileName(sample)));
            }

            await library.SaveDraftAsync(key, new CorrespondenceContent
            {
                Name = name,
                Dictionary = key,
                Document = await File.ReadAllBytesAsync(Path.Combine(from, "document.docx")),
                Email = await File.ReadAllTextAsync(Path.Combine(from, "email.md")),
                Sms = await File.ReadAllTextAsync(Path.Combine(from, "sms.txt")),
            }, expectedRevision: 0);
            await library.PublishAsync(key);
        }
    }
}
