using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;

namespace TradePilot.Api.Security;

public sealed class MtHmacValidator(
    ISourceSecretProvider sourceSecretProvider,
    INonceReplayGuard nonceReplayGuard,
    IOptions<HmacValidationOptions> options) : IMtHmacValidator
{
    public async Task<HmacValidationResult> ValidateAsync(HttpRequest request, CancellationToken cancellationToken)
    {
        if (!TryGetSingleHeader(request, HmacHeaders.SourceId, out var sourceId) || string.IsNullOrWhiteSpace(sourceId))
        {
            return HmacValidationResult.Failure("Missing or invalid X-Source-Id header.");
        }

        if (!TryGetSingleHeader(request, HmacHeaders.Timestamp, out var timestampRaw) || string.IsNullOrWhiteSpace(timestampRaw))
        {
            return HmacValidationResult.Failure("Missing or invalid X-Timestamp header.");
        }

        if (!TryGetSingleHeader(request, HmacHeaders.Nonce, out var nonce) || string.IsNullOrWhiteSpace(nonce))
        {
            return HmacValidationResult.Failure("Missing or invalid X-Nonce header.");
        }

        if (!TryGetSingleHeader(request, HmacHeaders.Signature, out var signatureRaw) || string.IsNullOrWhiteSpace(signatureRaw))
        {
            return HmacValidationResult.Failure("Missing or invalid X-Signature header.");
        }

        if (!sourceSecretProvider.TryGetSecret(sourceId, out var secret))
        {
            return HmacValidationResult.Failure("Unknown source or missing shared secret configuration.");
        }

        if (!long.TryParse(timestampRaw, out var unixTimestampSeconds))
        {
            return HmacValidationResult.Failure("Invalid X-Timestamp format.");
        }

        DateTimeOffset requestTimestamp;
        try
        {
            requestTimestamp = DateTimeOffset.FromUnixTimeSeconds(unixTimestampSeconds);
        }
        catch (ArgumentOutOfRangeException)
        {
            return HmacValidationResult.Failure("X-Timestamp is out of range.");
        }

        var allowedSkewSeconds = options.Value.AllowedClockSkewSeconds > 0
            ? options.Value.AllowedClockSkewSeconds
            : 300;
        var allowedSkew = TimeSpan.FromSeconds(allowedSkewSeconds);
        var nowUtc = DateTimeOffset.UtcNow;

        if ((nowUtc - requestTimestamp).Duration() > allowedSkew)
        {
            return HmacValidationResult.Failure("Request timestamp drift exceeded.");
        }

        var body = await ReadRequestBodyAsync(request, cancellationToken);
        var payload = $"{timestampRaw}.{nonce}.{body}";
        var expectedSignatureBytes = ComputeSignature(secret, payload);

        if (!TryParseProvidedSignature(signatureRaw, out var providedSignatureBytes))
        {
            return HmacValidationResult.Failure("X-Signature format is invalid.");
        }

        if (providedSignatureBytes.Length != expectedSignatureBytes.Length
            || !CryptographicOperations.FixedTimeEquals(providedSignatureBytes, expectedSignatureBytes))
        {
            return HmacValidationResult.Failure("Signature verification failed.");
        }

        if (!nonceReplayGuard.TryRegisterNonce(sourceId, nonce, nowUtc, allowedSkew))
        {
            return HmacValidationResult.Failure("Nonce replay detected.");
        }

        return HmacValidationResult.Success(sourceId);
    }

    private static async Task<string> ReadRequestBodyAsync(HttpRequest request, CancellationToken cancellationToken)
    {
        request.EnableBuffering();
        request.Body.Position = 0;

        using var reader = new StreamReader(request.Body, Encoding.UTF8, detectEncodingFromByteOrderMarks: false, leaveOpen: true);
        var body = await reader.ReadToEndAsync(cancellationToken);
        request.Body.Position = 0;
        return body;
    }

    private static bool TryGetSingleHeader(HttpRequest request, string headerName, out string value)
    {
        value = string.Empty;
        if (!request.Headers.TryGetValue(headerName, out var values) || values.Count != 1)
        {
            return false;
        }

        value = values[0]?.Trim() ?? string.Empty;
        return true;
    }

    private static byte[] ComputeSignature(string secret, string payload)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        return hmac.ComputeHash(Encoding.UTF8.GetBytes(payload));
    }

    private static bool TryParseProvidedSignature(string signatureRaw, out byte[] signatureBytes)
    {
        signatureBytes = Array.Empty<byte>();
        var normalized = signatureRaw.Trim();

        if (normalized.StartsWith("sha256=", StringComparison.OrdinalIgnoreCase))
        {
            normalized = normalized["sha256=".Length..].Trim();
        }

        if (IsHexDigest(normalized))
        {
            try
            {
                signatureBytes = Convert.FromHexString(normalized);
                return true;
            }
            catch (FormatException)
            {
                return false;
            }
        }

        try
        {
            signatureBytes = Convert.FromBase64String(normalized);
            return true;
        }
        catch (FormatException)
        {
            return false;
        }
    }

    private static bool IsHexDigest(string value)
    {
        if (value.Length != 64)
        {
            return false;
        }

        foreach (var c in value)
        {
            var isHexDigit = (c >= '0' && c <= '9')
                || (c >= 'a' && c <= 'f')
                || (c >= 'A' && c <= 'F');
            if (!isHexDigit)
            {
                return false;
            }
        }

        return true;
    }
}
