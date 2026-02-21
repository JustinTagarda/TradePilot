using System.Net;
using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using TradePilot.Connector.Configuration;
using TradePilot.Connector.Forwarding;
using TradePilot.Connector.Security;
using TradePilot.Shared.Models;
using TradePilot.Tests.Common;

namespace TradePilot.Tests.Connector;

public sealed class CloudSnapshotForwarderTests
{
    [Fact]
    public async Task ForwardAsync_AddsSignedHeaders_AndReturnsAccepted()
    {
        var secret = "connector-outbound-secret";
        using var captureHandler = new CaptureHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.Accepted));
        using var httpClient = new HttpClient(captureHandler)
        {
            BaseAddress = new Uri("http://localhost:5261")
        };

        var forwarder = CreateForwarder(httpClient, secret);
        var snapshot = CreateSnapshot("demo-source-01");

        var result = await forwarder.ForwardAsync(snapshot, CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(StatusCodes.Status202Accepted, result.StatusCode);
        Assert.NotNull(captureHandler.LastRequest);

        var request = captureHandler.LastRequest!;
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal("/v1/mt/snapshots", request.RequestUri!.AbsolutePath);

        var timestamp = request.Headers.GetValues(HmacHeaders.Timestamp).Single();
        var nonce = request.Headers.GetValues(HmacHeaders.Nonce).Single();
        var signature = request.Headers.GetValues(HmacHeaders.Signature).Single();
        var sourceId = request.Headers.GetValues(HmacHeaders.SourceId).Single();
        var body = await request.Content!.ReadAsStringAsync();

        Assert.Equal(snapshot.SourceId, sourceId);
        Assert.False(string.IsNullOrWhiteSpace(timestamp));
        Assert.False(string.IsNullOrWhiteSpace(nonce));
        Assert.False(string.IsNullOrWhiteSpace(signature));

        var expectedSignature = HmacTestHelper.ComputeHexSignature(secret, timestamp, nonce, body);
        Assert.Equal(expectedSignature, signature, ignoreCase: true);
    }

    [Fact]
    public async Task ForwardAsync_ReturnsFailure_WhenCloudApiRejectsRequest()
    {
        using var captureHandler = new CaptureHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.Unauthorized)
        {
            Content = new StringContent("invalid signature", Encoding.UTF8, "text/plain")
        });
        using var httpClient = new HttpClient(captureHandler)
        {
            BaseAddress = new Uri("http://localhost:5261")
        };

        var forwarder = CreateForwarder(httpClient, "connector-outbound-secret");
        var snapshot = CreateSnapshot("demo-source-01");

        var result = await forwarder.ForwardAsync(snapshot, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(StatusCodes.Status502BadGateway, result.StatusCode);
        Assert.Contains("Cloud API returned", result.Error ?? string.Empty);
    }

    private static CloudSnapshotForwarder CreateForwarder(HttpClient client, string secret)
    {
        var connectorOptions = Options.Create(new ConnectorOptions
        {
            CloudApiBaseUrl = "http://localhost:5261",
            SourceId = "connector-local"
        });

        var outboundOptions = Options.Create(new OutboundHmacOptions
        {
            SharedSecret = string.Empty,
            SourceSecrets = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["demo-source-01"] = secret
            }
        });

        var httpClientFactory = new StaticHttpClientFactory(client);
        var secretProvider = new ConfigurationOutboundSourceSecretProvider(outboundOptions);
        return new CloudSnapshotForwarder(
            httpClientFactory,
            connectorOptions,
            secretProvider,
            NullLogger<CloudSnapshotForwarder>.Instance);
    }

    private static MtSnapshot CreateSnapshot(string sourceId)
    {
        return new MtSnapshot
        {
            SourceId = sourceId,
            TimestampUtc = DateTime.UtcNow,
            Account = new MtAccount
            {
                Broker = "Demo",
                Server = "Server",
                Login = 1,
                Currency = "USD",
                Balance = 1,
                Equity = 1,
                Margin = 1,
                FreeMargin = 1,
                MarginLevel = 1
            },
            Positions = [],
            Orders = []
        };
    }

    private sealed class StaticHttpClientFactory(HttpClient client) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => client;
    }

    private sealed class CaptureHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> responder) : HttpMessageHandler
    {
        public HttpRequestMessage? LastRequest { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequest = await CloneRequestAsync(request);
            return responder(request);
        }

        private static async Task<HttpRequestMessage> CloneRequestAsync(HttpRequestMessage original)
        {
            var clone = new HttpRequestMessage(original.Method, original.RequestUri);
            foreach (var header in original.Headers)
            {
                clone.Headers.TryAddWithoutValidation(header.Key, header.Value);
            }

            if (original.Content is not null)
            {
                var contentBytes = await original.Content.ReadAsByteArrayAsync();
                clone.Content = new ByteArrayContent(contentBytes);
                foreach (var header in original.Content.Headers)
                {
                    clone.Content.Headers.TryAddWithoutValidation(header.Key, header.Value);
                }
            }

            return clone;
        }
    }
}
