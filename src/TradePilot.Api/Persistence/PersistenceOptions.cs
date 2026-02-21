namespace TradePilot.Api.Persistence;

public sealed class PersistenceOptions
{
    public const string SectionName = "Persistence";

    public bool Enabled { get; set; } = true;
    public string ConnectionString { get; set; } = "Data Source=tradepilot.db";
    public int RetentionDays { get; set; } = 30;
    public int DefaultHistoryTake { get; set; } = 200;
    public int MaxHistoryTake { get; set; } = 1000;
    public int RetentionCleanupIntervalWrites { get; set; } = 50;
}
