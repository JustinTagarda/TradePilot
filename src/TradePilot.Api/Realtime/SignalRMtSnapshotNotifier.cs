using Microsoft.AspNetCore.SignalR;
using TradePilot.Shared.Models;

namespace TradePilot.Api.Realtime;

public sealed class SignalRMtSnapshotNotifier(
    IHubContext<MtSnapshotsHub> hubContext) : IMtSnapshotNotifier
{
    public Task NotifySnapshotUpdatedAsync(MtSnapshot snapshot, CancellationToken cancellationToken)
    {
        var update = new MtSnapshotUpdate(snapshot.SourceId, snapshot.TimestampUtc);
        var groupName = MtSnapshotsHub.NormalizeGroupName(snapshot.SourceId);
        return hubContext.Clients.Group(groupName).SendAsync("SnapshotUpdated", update, cancellationToken);
    }
}
