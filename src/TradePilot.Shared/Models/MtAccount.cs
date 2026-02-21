namespace TradePilot.Shared.Models;

public sealed class MtAccount
{
    public string Broker { get; set; } = string.Empty;
    public string Server { get; set; } = string.Empty;
    public long Login { get; set; }
    public string Currency { get; set; } = string.Empty;
    public decimal Balance { get; set; }
    public decimal Equity { get; set; }
    public decimal Margin { get; set; }
    public decimal FreeMargin { get; set; }
    public decimal MarginLevel { get; set; }
}
