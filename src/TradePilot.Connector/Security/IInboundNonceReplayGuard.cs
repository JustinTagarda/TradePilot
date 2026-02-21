namespace TradePilot.Connector.Security;

public interface IInboundNonceReplayGuard
{
    bool TryRegisterNonce(string sourceId, string nonce, DateTimeOffset nowUtc, TimeSpan ttl);
}
