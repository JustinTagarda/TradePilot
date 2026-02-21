using Microsoft.Extensions.Options;

namespace TradePilot.Connector.Forwarding;

public sealed class ConfigurationOutboundSourceSecretProvider(IOptions<OutboundHmacOptions> options) : IOutboundSourceSecretProvider
{
    public bool TryGetSecret(string sourceId, out string secret)
    {
        var outboundOptions = options.Value;
        if (outboundOptions.SourceSecrets.TryGetValue(sourceId, out var sourceSecret) && !string.IsNullOrWhiteSpace(sourceSecret))
        {
            secret = sourceSecret;
            return true;
        }

        if (!string.IsNullOrWhiteSpace(outboundOptions.SharedSecret))
        {
            secret = outboundOptions.SharedSecret;
            return true;
        }

        secret = string.Empty;
        return false;
    }
}
