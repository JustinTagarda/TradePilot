namespace TradePilot.Connector.Configuration;

public sealed class ConnectorOptions
{
    public const string SectionName = "Connector";

    public string CloudApiBaseUrl { get; set; } = "http://localhost:5261";
    public string SourceId { get; set; } = "connector-local";
}
