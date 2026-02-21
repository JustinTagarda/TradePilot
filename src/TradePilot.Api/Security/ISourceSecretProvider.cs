namespace TradePilot.Api.Security;

public interface ISourceSecretProvider
{
    bool TryGetSecret(string sourceId, out string secret);
}
