using Microsoft.EntityFrameworkCore;

namespace TradePilot.Api.Persistence;

public sealed class TradePilotDbContext(DbContextOptions<TradePilotDbContext> options) : DbContext(options)
{
    public DbSet<SnapshotHistoryRecord> SnapshotHistory => Set<SnapshotHistoryRecord>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var history = modelBuilder.Entity<SnapshotHistoryRecord>();
        history.ToTable("SnapshotHistory");
        history.HasKey(x => x.Id);
        history.Property(x => x.SourceId).IsRequired().HasMaxLength(128);
        history.Property(x => x.TimestampUtc).IsRequired();
        history.Property(x => x.ReceivedUtc).IsRequired();
        history.Property(x => x.PayloadJson).IsRequired();
        history.HasIndex(x => new { x.SourceId, x.TimestampUtc });
        history.HasIndex(x => x.ReceivedUtc);
    }
}
