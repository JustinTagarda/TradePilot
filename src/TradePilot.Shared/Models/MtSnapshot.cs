namespace TradePilot.Shared.Models;

public sealed class MtSnapshot
{
    public string SourceId { get; set; } = string.Empty;
    public DateTime TimestampUtc { get; set; }
    public MtAccount Account { get; set; } = new();
    public List<MtPosition> Positions { get; set; } = [];
    public List<MtOrder> Orders { get; set; } = [];
}
