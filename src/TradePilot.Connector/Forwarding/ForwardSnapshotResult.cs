namespace TradePilot.Connector.Forwarding;

public sealed record ForwardSnapshotResult(bool Success, int StatusCode, string? Error)
{
    public static ForwardSnapshotResult Accepted() => new(true, StatusCodes.Status202Accepted, null);

    public static ForwardSnapshotResult Failed(int statusCode, string error) => new(false, statusCode, error);
}
