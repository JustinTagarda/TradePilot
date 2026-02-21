using System.Net;
using System.Net.Http.Json;
using TradePilot.Shared.Models;
using TradePilot.Shared.Serialization;
using TradePilot.Web.Models;

namespace TradePilot.Web.Services;

public sealed class MtApiClient(HttpClient httpClient) : IMtApiClient
{
    public async Task<IReadOnlyList<MtSourceSummary>> GetSourcesAsync(CancellationToken cancellationToken = default)
    {
        var sources = await httpClient.GetFromJsonAsync<List<MtSourceSummary>>(
            "/v1/mt/sources",
            TradePilotJson.Default,
            cancellationToken);

        return sources ?? [];
    }

    public async Task<MtSnapshot?> GetLatestSnapshotAsync(string sourceId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(sourceId))
        {
            throw new ArgumentException("sourceId is required.", nameof(sourceId));
        }

        var response = await httpClient.GetAsync($"/v1/mt/sources/{Uri.EscapeDataString(sourceId)}/latest", cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<MtSnapshot>(TradePilotJson.Default, cancellationToken);
    }
}
