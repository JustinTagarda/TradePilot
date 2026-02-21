namespace TradePilot.Api.Security;

public sealed class HmacValidationOptions
{
    public const string SectionName = "Security:Hmac";

    public int AllowedClockSkewSeconds { get; set; } = 300;
    public string SharedSecret { get; set; } = string.Empty;
    public Dictionary<string, string> SourceSecrets { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}
