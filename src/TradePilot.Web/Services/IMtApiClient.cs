using TradePilot.Shared.Models;
using TradePilot.Web.Models;

namespace TradePilot.Web.Services;

public interface IMtApiClient
{
    Task<IReadOnlyList<MtSourceSummary>> GetSourcesAsync(CancellationToken cancellationToken = default);

    Task<MtSnapshot?> GetLatestSnapshotAsync(string sourceId, CancellationToken cancellationToken = default);
}
