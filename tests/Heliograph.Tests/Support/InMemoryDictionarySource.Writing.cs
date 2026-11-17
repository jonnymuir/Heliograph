using Heliograph.Library;

namespace Heliograph.Tests.Support;

/// <summary>Writing to the in-memory dictionaries, as an author does from the Dictionaries screen.</summary>
internal sealed partial class InMemoryDictionarySource : IDictionaryStore
{
    public Task SaveAsync(string name, string json, CancellationToken cancellationToken = default)
    {
        _dictionaries[name] = json;
        return Task.CompletedTask;
    }

    public Task SaveSampleAsync(string name, string sample, string json, CancellationToken cancellationToken = default)
    {
        _samples[(name, sample)] = json;
        return Task.CompletedTask;
    }

    public Task DeleteSampleAsync(string name, string sample, CancellationToken cancellationToken = default)
    {
        _samples.Remove((name, sample));
        return Task.CompletedTask;
    }
}
