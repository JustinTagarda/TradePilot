using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using TradePilot.Api.Persistence;
using TradePilot.Shared.Models;
using TradePilot.Shared.Serialization;

namespace TradePilot.Api.Snapshots;

public sealed class SqliteSnapshotHistoryStore(
    IDbContextFactory<TradePilotDbContext> dbContextFactory,
    IOptions<PersistenceOptions> persistenceOptions,
    ILogger<SqliteSnapshotHistoryStore> logger) : ISnapshotHistoryStore
{
    private int _writeCounter;

    public async Task PersistAsync(MtSnapshot snapshot, CancellationToken cancellationToken)
    {
        var options = persistenceOptions.Value;
        if (!options.Enabled)
        {
            return;
        }

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var payloadJson = JsonSerializer.Serialize(snapshot, TradePilotJson.Default);

        dbContext.SnapshotHistory.Add(new SnapshotHistoryRecord
        {
            SourceId = snapshot.SourceId,
            TimestampUtc = snapshot.TimestampUtc,
            ReceivedUtc = DateTime.UtcNow,
            PayloadJson = payloadJson
        });

        await dbContext.SaveChangesAsync(cancellationToken);
        await CleanupExpiredRecordsIfNeededAsync(dbContext, options, cancellationToken);
    }

    public async Task<IReadOnlyList<MtSnapshot>> GetHistoryAsync(string sourceId, int? take, CancellationToken cancellationToken)
    {
        var options = persistenceOptions.Value;
        if (!options.Enabled || string.IsNullOrWhiteSpace(sourceId))
        {
            return [];
        }

        var normalizedTake = NormalizeTake(take, options);

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var payloads = await dbContext.SnapshotHistory
            .AsNoTracking()
            .Where(record => record.SourceId == sourceId)
            .OrderByDescending(record => record.TimestampUtc)
            .Take(normalizedTake)
            .Select(record => record.PayloadJson)
            .ToListAsync(cancellationToken);

        var results = new List<MtSnapshot>(payloads.Count);
        foreach (var payload in payloads)
        {
            try
            {
                var snapshot = JsonSerializer.Deserialize<MtSnapshot>(payload, TradePilotJson.Default);
                if (snapshot is not null)
                {
                    results.Add(snapshot);
                }
            }
            catch (JsonException ex)
            {
                logger.LogWarning(ex, "Failed to deserialize snapshot history payload.");
            }
        }

        return results;
    }

    private async Task CleanupExpiredRecordsIfNeededAsync(
        TradePilotDbContext dbContext,
        PersistenceOptions options,
        CancellationToken cancellationToken)
    {
        if (options.RetentionDays <= 0)
        {
            return;
        }

        var cleanupInterval = options.RetentionCleanupIntervalWrites > 0
            ? options.RetentionCleanupIntervalWrites
            : 50;

        if (Interlocked.Increment(ref _writeCounter) % cleanupInterval != 0)
        {
            return;
        }

        var cutoffUtc = DateTime.UtcNow.AddDays(-options.RetentionDays);
        var deleted = await dbContext.SnapshotHistory
            .Where(record => record.ReceivedUtc < cutoffUtc)
            .ExecuteDeleteAsync(cancellationToken);

        if (deleted > 0)
        {
            logger.LogInformation("Deleted {DeletedCount} expired snapshot history records.", deleted);
        }
    }

    private static int NormalizeTake(int? take, PersistenceOptions options)
    {
        var defaultTake = options.DefaultHistoryTake > 0 ? options.DefaultHistoryTake : 200;
        var maxTake = options.MaxHistoryTake > 0 ? options.MaxHistoryTake : 1000;
        var requestedTake = take.GetValueOrDefault(defaultTake);
        if (requestedTake <= 0)
        {
            requestedTake = defaultTake;
        }

        return Math.Min(requestedTake, maxTake);
    }
}
