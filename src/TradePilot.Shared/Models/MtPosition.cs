namespace TradePilot.Shared.Models;

public sealed class MtPosition
{
    public long Ticket { get; set; }
    public string Symbol { get; set; } = string.Empty;
    public string Side { get; set; } = string.Empty;
    public decimal Volume { get; set; }
    public decimal OpenPrice { get; set; }
    public decimal StopLoss { get; set; }
    public decimal TakeProfit { get; set; }
    public decimal CurrentPrice { get; set; }
    public decimal Profit { get; set; }
}
