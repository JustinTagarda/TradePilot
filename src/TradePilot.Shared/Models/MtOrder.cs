namespace TradePilot.Shared.Models;

public sealed class MtOrder
{
    public long Ticket { get; set; }
    public string Symbol { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public decimal Volume { get; set; }
    public decimal Price { get; set; }
    public decimal StopLoss { get; set; }
    public decimal TakeProfit { get; set; }
    public DateTime TimeUtc { get; set; }
}
