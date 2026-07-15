namespace Xavissa.Frontend.Models;

public sealed class OfflineFirstOptions
{
    public const string SectionName = "OfflineFirst";

    public int LowStockThreshold { get; set; } = 5;
    public int BackgroundSyncIntervalSeconds { get; set; } = 180;
    public string StockConflictPolicy { get; set; } = "AlertStaff";
}
