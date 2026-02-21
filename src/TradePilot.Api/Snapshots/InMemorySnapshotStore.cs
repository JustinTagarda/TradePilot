using System.Collections.Concurrent;
using TradePilot.Shared.Models;

namespace TradePilot.Api.Snapshots;

public sealed class InMemorySnapshotStore : ISnapshotStore
{
    private readonly ConcurrentDictionary<string, MtSnapshot> _latestBySource =
        new(StringComparer.OrdinalIgnoreCase);

    public void Upsert(MtSnapshot snapshot)
    {
        _latestBySource[snapshot.SourceId] = snapshot;
    }

    public IReadOnlyList<MtSourceSummary> GetSources()
    {
        return _latestBySource.Values
            .OrderBy(snapshot => snapshot.SourceId, StringComparer.OrdinalIgnoreCase)
            .Select(snapshot => new MtSourceSummary(snapshot.SourceId, snapshot.TimestampUtc))
            .ToArray();
    }

    public bool TryGetLatest(string sourceId, out MtSnapshot snapshot)
    {
        if (_latestBySource.TryGetValue(sourceId, out var found))
        {
            snapshot = found;
            return true;
        }

        snapshot = null!;
        return false;
    }
}
