using TradePilot.Shared.Models;

namespace TradePilot.Api.Snapshots;

public interface ISnapshotStore
{
    void Upsert(MtSnapshot snapshot);

    IReadOnlyList<MtSourceSummary> GetSources();

    bool TryGetLatest(string sourceId, out MtSnapshot snapshot);
}
