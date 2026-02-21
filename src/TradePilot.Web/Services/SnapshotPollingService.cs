using TradePilot.Shared.Models;

namespace TradePilot.Web.Services;

public sealed class SnapshotPollingService(IMtApiClient apiClient) : ISnapshotPollingService
{
    public async Task PollLatestSnapshotAsync(
        string sourceId,
        TimeSpan interval,
        Func<MtSnapshot?, Task> onSnapshot,
        Func<Exception, Task>? onError,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(sourceId))
        {
            throw new ArgumentException("sourceId is required.", nameof(sourceId));
        }

        if (interval <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(interval), "Polling interval must be greater than zero.");
        }

        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                var snapshot = await apiClient.GetLatestSnapshotAsync(sourceId, cancellationToken);
                await onSnapshot(snapshot);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                if (onError is not null)
                {
                    await onError(ex);
                }
            }

            try
            {
                await Task.Delay(interval, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
        }
    }
}
