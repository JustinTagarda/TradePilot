using TradePilot.Shared.Models;

namespace TradePilot.Connector.Forwarding;

public interface ICloudSnapshotForwarder
{
    Task<ForwardSnapshotResult> ForwardAsync(MtSnapshot snapshot, CancellationToken cancellationToken);
}
