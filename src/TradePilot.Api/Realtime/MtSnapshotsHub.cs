using Microsoft.AspNetCore.SignalR;

namespace TradePilot.Api.Realtime;

public sealed class MtSnapshotsHub : Hub
{
    public Task SubscribeSource(string sourceId)
    {
        if (string.IsNullOrWhiteSpace(sourceId))
        {
            return Task.CompletedTask;
        }

        return Groups.AddToGroupAsync(Context.ConnectionId, NormalizeGroupName(sourceId));
    }

    public Task UnsubscribeSource(string sourceId)
    {
        if (string.IsNullOrWhiteSpace(sourceId))
        {
            return Task.CompletedTask;
        }

        return Groups.RemoveFromGroupAsync(Context.ConnectionId, NormalizeGroupName(sourceId));
    }

    public static string NormalizeGroupName(string sourceId) =>
        sourceId.Trim().ToLowerInvariant();
}
