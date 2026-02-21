using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using TradePilot.Shared.Models;
using TradePilot.Shared.Serialization;
using TradePilot.Tests.Common;

namespace TradePilot.Tests.Api;

public sealed class ApiEndpointsTests : IClassFixture<ApiWebApplicationFactory>
{
    private readonly ApiWebApplicationFactory _factory;

    public ApiEndpointsTests(ApiWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task HealthEndpoint_ReturnsOk()
    {
        using var client = _factory.CreateClient();

        var response = await client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task SnapshotEndpoints_AcceptSnapshot_AndExposeSourcesAndLatest()
    {
        using var client = _factory.CreateClient();

        var sourceId = "demo-source-01";
        var secret = ApiWebApplicationFactory.SourceSecret;
        var snapshot = new MtSnapshot
        {
            SourceId = sourceId,
            TimestampUtc = DateTime.UtcNow,
            Account = new MtAccount
            {
                Broker = "Demo",
                Server = "Server",
                Login = 12345,
                Currency = "USD",
                Balance = 10000,
                Equity = 10010,
                Margin = 100,
                FreeMargin = 9910,
                MarginLevel = 10010
            },
            Positions = [],
            Orders = []
        };

        var body = JsonSerializer.Serialize(snapshot, TradePilotJson.Default);
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();
        var nonce = Guid.NewGuid().ToString("N");
        var signature = HmacTestHelper.ComputeHexSignature(secret, timestamp, nonce, body);

        using var ingestRequest = new HttpRequestMessage(HttpMethod.Post, "/v1/mt/snapshots")
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json")
        };
        ingestRequest.Headers.Add("X-Source-Id", sourceId);
        ingestRequest.Headers.Add("X-Timestamp", timestamp);
        ingestRequest.Headers.Add("X-Nonce", nonce);
        ingestRequest.Headers.Add("X-Signature", signature);

        var ingestResponse = await client.SendAsync(ingestRequest);
        Assert.Equal(HttpStatusCode.Accepted, ingestResponse.StatusCode);

        var sourcesResponse = await client.GetAsync("/v1/mt/sources");
        Assert.Equal(HttpStatusCode.OK, sourcesResponse.StatusCode);
        var sources = await sourcesResponse.Content.ReadFromJsonAsync<List<WebSourceSummary>>(TradePilotJson.Default);
        Assert.NotNull(sources);
        Assert.Contains(sources!, s => string.Equals(s.SourceId, sourceId, StringComparison.OrdinalIgnoreCase));

        var latestResponse = await client.GetAsync($"/v1/mt/sources/{sourceId}/latest");
        Assert.Equal(HttpStatusCode.OK, latestResponse.StatusCode);
        var latestSnapshot = await latestResponse.Content.ReadFromJsonAsync<MtSnapshot>(TradePilotJson.Default);
        Assert.NotNull(latestSnapshot);
        Assert.Equal(sourceId, latestSnapshot!.SourceId);

        var historyResponse = await client.GetAsync($"/v1/mt/sources/{sourceId}/history?take=10");
        Assert.Equal(HttpStatusCode.OK, historyResponse.StatusCode);
        var history = await historyResponse.Content.ReadFromJsonAsync<List<MtSnapshot>>(TradePilotJson.Default);
        Assert.NotNull(history);
        Assert.NotEmpty(history!);
        Assert.Equal(sourceId, history![0].SourceId);
    }

    private sealed class WebSourceSummary
    {
        public string SourceId { get; set; } = string.Empty;
        public DateTime TimestampUtc { get; set; }
    }
}

public sealed class ApiWebApplicationFactory : WebApplicationFactory<Program>
{
    public const string SourceSecret = "api-integration-secret";
    private readonly string _databasePath = Path.Combine(Path.GetTempPath(), $"tradepilot-tests-{Guid.NewGuid():N}.db");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration((_, configBuilder) =>
        {
            var inMemorySettings = new Dictionary<string, string?>
            {
                ["Security:Hmac:AllowedClockSkewSeconds"] = "300",
                ["Security:Hmac:SharedSecret"] = "",
                ["Security:Hmac:SourceSecrets:demo-source-01"] = SourceSecret,
                ["Persistence:Enabled"] = "true",
                ["Persistence:ConnectionString"] = $"Data Source={_databasePath}",
                ["Persistence:RetentionDays"] = "30"
            };
            configBuilder.AddInMemoryCollection(inMemorySettings);
        });
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        if (!File.Exists(_databasePath))
        {
            return;
        }

        for (var attempt = 0; attempt < 5; attempt++)
        {
            try
            {
                File.Delete(_databasePath);
                return;
            }
            catch (IOException) when (attempt < 4)
            {
                System.Threading.Thread.Sleep(100);
            }
            catch (UnauthorizedAccessException) when (attempt < 4)
            {
                System.Threading.Thread.Sleep(100);
            }
            catch (IOException)
            {
                return;
            }
            catch (UnauthorizedAccessException)
            {
                return;
            }
        }
    }
}
