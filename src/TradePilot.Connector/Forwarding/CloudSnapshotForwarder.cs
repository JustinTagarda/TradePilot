using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using TradePilot.Connector.Configuration;
using TradePilot.Connector.Security;
using TradePilot.Shared.Models;
using TradePilot.Shared.Serialization;

namespace TradePilot.Connector.Forwarding;

public sealed class CloudSnapshotForwarder(
    IHttpClientFactory httpClientFactory,
    IOptions<ConnectorOptions> connectorOptions,
    IOutboundSourceSecretProvider sourceSecretProvider,
    ILogger<CloudSnapshotForwarder> logger) : ICloudSnapshotForwarder
{
    public const string HttpClientName = "CloudApi";

    public async Task<ForwardSnapshotResult> ForwardAsync(MtSnapshot snapshot, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(snapshot.SourceId))
        {
            return ForwardSnapshotResult.Failed(StatusCodes.Status400BadRequest, "Snapshot sourceId is required.");
        }

        if (!sourceSecretProvider.TryGetSecret(snapshot.SourceId, out var secret))
        {
            return ForwardSnapshotResult.Failed(
                StatusCodes.Status500InternalServerError,
                "Outbound HMAC secret is not configured for this source.");
        }

        var cloudApiBaseUrl = connectorOptions.Value.CloudApiBaseUrl;
        if (!Uri.TryCreate(cloudApiBaseUrl, UriKind.Absolute, out _))
        {
            return ForwardSnapshotResult.Failed(
                StatusCodes.Status500InternalServerError,
                "Connector cloud API base URL is invalid.");
        }

        var jsonBody = JsonSerializer.Serialize(snapshot, TradePilotJson.Default);
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture);
        var nonce = Guid.NewGuid().ToString("N");
        var payload = $"{timestamp}.{nonce}.{jsonBody}";
        var signature = ComputeHexSignature(secret, payload);

        var request = new HttpRequestMessage(HttpMethod.Post, "/v1/mt/snapshots")
        {
            Content = new StringContent(jsonBody, Encoding.UTF8, "application/json")
        };
        request.Headers.Add(HmacHeaders.SourceId, snapshot.SourceId);
        request.Headers.Add(HmacHeaders.Timestamp, timestamp);
        request.Headers.Add(HmacHeaders.Nonce, nonce);
        request.Headers.Add(HmacHeaders.Signature, signature);

        try
        {
            var httpClient = httpClientFactory.CreateClient(HttpClientName);
            var response = await httpClient.SendAsync(request, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var detail = await response.Content.ReadAsStringAsync(cancellationToken);
                logger.LogWarning(
                    "Cloud API rejected forwarded snapshot. StatusCode={StatusCode}, SourceId={SourceId}, Body={Body}",
                    (int)response.StatusCode,
                    snapshot.SourceId,
                    detail);

                return ForwardSnapshotResult.Failed(
                    StatusCodes.Status502BadGateway,
                    $"Cloud API returned {(int)response.StatusCode}.");
            }

            return ForwardSnapshotResult.Accepted();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to forward snapshot to Cloud API for source {SourceId}.", snapshot.SourceId);
            return ForwardSnapshotResult.Failed(StatusCodes.Status502BadGateway, "Cloud API forward request failed.");
        }
    }

    private static string ComputeHexSignature(string secret, string payload)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var signatureBytes = hmac.ComputeHash(Encoding.UTF8.GetBytes(payload));
        return Convert.ToHexString(signatureBytes).ToLowerInvariant();
    }
}
