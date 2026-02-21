using TradePilot.Shared.Models;

namespace TradePilot.Web.Services;

public interface ISnapshotPollingService
{
    Task PollLatestSnapshotAsync(
        string sourceId,
        TimeSpan interval,
        Func<MtSnapshot?, Task> onSnapshot,
        Func<Exception, Task>? onError,
        CancellationToken cancellationToken = default);
}
