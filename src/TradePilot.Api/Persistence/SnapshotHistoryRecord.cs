namespace TradePilot.Api.Persistence;

public sealed class SnapshotHistoryRecord
{
    public long Id { get; set; }
    public string SourceId { get; set; } = string.Empty;
    public DateTime TimestampUtc { get; set; }
    public DateTime ReceivedUtc { get; set; }
    public string PayloadJson { get; set; } = string.Empty;
}
