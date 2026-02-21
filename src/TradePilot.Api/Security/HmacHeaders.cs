namespace TradePilot.Api.Security;

public static class HmacHeaders
{
    public const string SourceId = "X-Source-Id";
    public const string Timestamp = "X-Timestamp";
    public const string Nonce = "X-Nonce";
    public const string Signature = "X-Signature";
}
