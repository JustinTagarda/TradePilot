namespace TradePilot.Connector.Security;

public sealed class InboundHmacValidationOptions
{
    public const string SectionName = "Security:InboundHmac";

    public int AllowedClockSkewSeconds { get; set; } = 300;
    public string SharedSecret { get; set; } = string.Empty;
    public Dictionary<string, string> SourceSecrets { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}
