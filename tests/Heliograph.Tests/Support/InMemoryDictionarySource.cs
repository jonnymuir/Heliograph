using Heliograph.Library;

namespace Heliograph.Tests.Support;

/// <summary>Dictionaries held in memory, which a test can change as the people who own the data would.</summary>
internal sealed partial class InMemoryDictionarySource : IDictionarySource
{
    private readonly SortedDictionary<string, string> _dictionaries = new(StringComparer.Ordinal);
    private readonly SortedDictionary<(string Dictionary, string Sample), string> _samples = [];

    public string this[string name]
    {
        set => _dictionaries[name] = value;
    }

    public void Sample(string dictionary, string sample, string json) => _samples[(dictionary, sample)] = json;

    public Task<IReadOnlyList<string>> ListSamplesAsync(string name, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<string>>([.. _samples.Keys.Where(k => k.Dictionary == name).Select(k => k.Sample)]);

    public Task<string?> ReadSampleAsync(string name, string sample, CancellationToken cancellationToken = default) =>
        Task.FromResult(_samples.GetValueOrDefault((name, sample)));

    public Task<IReadOnlyList<string>> ListAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<string>>([.. _dictionaries.Keys]);

    public Task<string?> ReadAsync(string name, CancellationToken cancellationToken = default) =>
        Task.FromResult(_dictionaries.GetValueOrDefault(name));
}
