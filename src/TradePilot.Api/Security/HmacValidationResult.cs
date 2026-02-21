namespace TradePilot.Api.Security;

public sealed record HmacValidationResult(bool IsValid, string? Error, string? SourceId)
{
    public static HmacValidationResult Success(string sourceId) => new(true, null, sourceId);
    public static HmacValidationResult Failure(string error) => new(false, error, null);
}
