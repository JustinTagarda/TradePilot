using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using TradePilot.Api.Security;
using TradePilot.Tests.Common;

namespace TradePilot.Tests.Api;

public sealed class MtHmacValidatorTests
{
    private const string SourceId = "demo-source-01";
    private const string Secret = "test-secret";
    private const string Body = "{\"sourceId\":\"demo-source-01\"}";

    [Fact]
    public async Task ValidateAsync_ReturnsSuccess_ForValidSignature()
    {
        var validator = CreateValidator();
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();
        var nonce = "nonce-valid-01";
        var signature = HmacTestHelper.ComputeHexSignature(Secret, timestamp, nonce, Body);
        var request = CreateRequest(SourceId, timestamp, nonce, signature, Body);

        var result = await validator.ValidateAsync(request, CancellationToken.None);

        Assert.True(result.IsValid);
        Assert.Equal(SourceId, result.SourceId);
    }

    [Fact]
    public async Task ValidateAsync_ReturnsFailure_WhenNonceIsReplayed()
    {
        var validator = CreateValidator();
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();
        var nonce = "nonce-replay-01";
        var signature = HmacTestHelper.ComputeHexSignature(Secret, timestamp, nonce, Body);

        var firstRequest = CreateRequest(SourceId, timestamp, nonce, signature, Body);
        var secondRequest = CreateRequest(SourceId, timestamp, nonce, signature, Body);

        var firstResult = await validator.ValidateAsync(firstRequest, CancellationToken.None);
        var secondResult = await validator.ValidateAsync(secondRequest, CancellationToken.None);

        Assert.True(firstResult.IsValid);
        Assert.False(secondResult.IsValid);
        Assert.Contains("Nonce replay", secondResult.Error ?? string.Empty, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ValidateAsync_ReturnsFailure_WhenTimestampDriftExceeded()
    {
        var validator = CreateValidator();
        var timestamp = DateTimeOffset.UtcNow.AddHours(-1).ToUnixTimeSeconds().ToString();
        var nonce = "nonce-drift-01";
        var signature = HmacTestHelper.ComputeHexSignature(Secret, timestamp, nonce, Body);
        var request = CreateRequest(SourceId, timestamp, nonce, signature, Body);

        var result = await validator.ValidateAsync(request, CancellationToken.None);

        Assert.False(result.IsValid);
        Assert.Contains("drift", result.Error ?? string.Empty, StringComparison.OrdinalIgnoreCase);
    }

    private static MtHmacValidator CreateValidator()
    {
        var options = Options.Create(new HmacValidationOptions
        {
            AllowedClockSkewSeconds = 300,
            SharedSecret = string.Empty,
            SourceSecrets = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                [SourceId] = Secret
            }
        });

        return new MtHmacValidator(
            new ConfigurationSourceSecretProvider(options),
            new MemoryNonceReplayGuard(),
            options);
    }

    private static HttpRequest CreateRequest(string sourceId, string timestamp, string nonce, string signature, string body)
    {
        var context = new DefaultHttpContext();
        var request = context.Request;
        request.Method = HttpMethods.Post;
        request.Path = "/v1/mt/snapshots";
        request.ContentType = "application/json";
        request.Headers[HmacHeaders.SourceId] = sourceId;
        request.Headers[HmacHeaders.Timestamp] = timestamp;
        request.Headers[HmacHeaders.Nonce] = nonce;
        request.Headers[HmacHeaders.Signature] = signature;
        request.Body = new MemoryStream(Encoding.UTF8.GetBytes(body));
        return request;
    }
}
