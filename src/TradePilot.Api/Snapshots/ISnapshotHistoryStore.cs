using TradePilot.Shared.Models;

namespace TradePilot.Api.Snapshots;

public interface ISnapshotHistoryStore
{
    Task PersistAsync(MtSnapshot snapshot, CancellationToken cancellationToken);

    Task<IReadOnlyList<MtSnapshot>> GetHistoryAsync(string sourceId, int? take, CancellationToken cancellationToken);
}
