using Heliograph.Library;

namespace Heliograph.Tests.Support;

/// <summary>A store that keeps correspondence in memory, as a host's store would in a database.</summary>
internal sealed class InMemoryCorrespondenceStore : ICorrespondenceStore
{
    private readonly Lock _gate = new();
    private readonly SortedDictionary<string, CorrespondenceDraft> _drafts = new(StringComparer.Ordinal);
    private readonly Dictionary<string, List<PublishedCorrespondence>> _versions = [];

    public Task<IReadOnlyList<CorrespondenceSummary>> ListAsync(CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            return Task.FromResult<IReadOnlyList<CorrespondenceSummary>>([.. _drafts.Values.Select(draft => new CorrespondenceSummary(
                draft.Key, draft.Content.Name, draft.Content.Dictionary, _versions.GetValueOrDefault(draft.Key)?.Count))]);
        }
    }

    public Task<CorrespondenceDraft?> ReadDraftAsync(string key, CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            return Task.FromResult(_drafts.GetValueOrDefault(key));
        }
    }

    public Task<int?> SaveDraftAsync(string key, CorrespondenceContent content, int expectedRevision, CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            if ((_drafts.GetValueOrDefault(key)?.Revision ?? 0) != expectedRevision)
            {
                return Task.FromResult<int?>(null);
            }

            _drafts[key] = new CorrespondenceDraft(key, content, expectedRevision + 1);
            return Task.FromResult<int?>(expectedRevision + 1);
        }
    }

    public Task<PublishedCorrespondence> AddVersionAsync(
        string key, CorrespondenceContent content, string dictionaryJson, DateTimeOffset publishedAt, CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            if (!_versions.TryGetValue(key, out var versions))
            {
                _versions[key] = versions = [];
            }

            var published = new PublishedCorrespondence(key, versions.Count + 1, content, dictionaryJson, publishedAt);
            versions.Add(published);
            return Task.FromResult(published);
        }
    }

    public Task<PublishedCorrespondence?> ReadVersionAsync(string key, int? version = null, CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            var versions = _versions.GetValueOrDefault(key) ?? [];
            return Task.FromResult(version is null ? versions.LastOrDefault() : versions.FirstOrDefault(v => v.Version == version));
        }
    }
}

/// <summary>A clock a test sets.</summary>
internal sealed class FixedClock(DateTimeOffset now) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => now;
}
