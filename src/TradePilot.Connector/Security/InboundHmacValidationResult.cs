namespace TradePilot.Connector.Security;

public sealed record InboundHmacValidationResult(bool IsValid, string? Error, string? SourceId)
{
    public static InboundHmacValidationResult Success(string sourceId) => new(true, null, sourceId);
    public static InboundHmacValidationResult Failure(string error) => new(false, error, null);
}
