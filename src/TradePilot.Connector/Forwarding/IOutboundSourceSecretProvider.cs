namespace TradePilot.Connector.Forwarding;

public interface IOutboundSourceSecretProvider
{
    bool TryGetSecret(string sourceId, out string secret);
}
