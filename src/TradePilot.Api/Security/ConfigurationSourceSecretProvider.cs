using Microsoft.Extensions.Options;

namespace TradePilot.Api.Security;

public sealed class ConfigurationSourceSecretProvider(IOptions<HmacValidationOptions> options) : ISourceSecretProvider
{
    public bool TryGetSecret(string sourceId, out string secret)
    {
        var hmacOptions = options.Value;
        if (hmacOptions.SourceSecrets.TryGetValue(sourceId, out var sourceSecret) && !string.IsNullOrWhiteSpace(sourceSecret))
        {
            secret = sourceSecret;
            return true;
        }

        if (!string.IsNullOrWhiteSpace(hmacOptions.SharedSecret))
        {
            secret = hmacOptions.SharedSecret;
            return true;
        }

        secret = string.Empty;
        return false;
    }
}
