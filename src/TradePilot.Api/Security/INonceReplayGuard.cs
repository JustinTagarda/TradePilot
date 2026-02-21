namespace TradePilot.Api.Security;

public interface INonceReplayGuard
{
    bool TryRegisterNonce(string sourceId, string nonce, DateTimeOffset nowUtc, TimeSpan ttl);
}
