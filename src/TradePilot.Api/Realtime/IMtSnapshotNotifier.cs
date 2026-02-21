using TradePilot.Shared.Models;

namespace TradePilot.Api.Realtime;

public interface IMtSnapshotNotifier
{
    Task NotifySnapshotUpdatedAsync(MtSnapshot snapshot, CancellationToken cancellationToken);
}
