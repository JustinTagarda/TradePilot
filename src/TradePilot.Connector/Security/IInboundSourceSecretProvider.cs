namespace TradePilot.Connector.Security;

public interface IInboundSourceSecretProvider
{
    bool TryGetSecret(string sourceId, out string secret);
}
