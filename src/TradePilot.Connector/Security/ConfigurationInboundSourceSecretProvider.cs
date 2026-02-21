using Microsoft.Extensions.Options;

namespace TradePilot.Connector.Security;

public sealed class ConfigurationInboundSourceSecretProvider(IOptions<InboundHmacValidationOptions> options) : IInboundSourceSecretProvider
{
    public bool TryGetSecret(string sourceId, out string secret)
    {
        var inboundOptions = options.Value;
        if (inboundOptions.SourceSecrets.TryGetValue(sourceId, out var sourceSecret) && !string.IsNullOrWhiteSpace(sourceSecret))
        {
            secret = sourceSecret;
            return true;
        }

        if (!string.IsNullOrWhiteSpace(inboundOptions.SharedSecret))
        {
            secret = inboundOptions.SharedSecret;
            return true;
        }

        secret = string.Empty;
        return false;
    }
}
