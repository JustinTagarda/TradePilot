namespace TradePilot.Connector.Forwarding;

public sealed class OutboundHmacOptions
{
    public const string SectionName = "Security:OutboundHmac";

    public string SharedSecret { get; set; } = string.Empty;
    public Dictionary<string, string> SourceSecrets { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}
