namespace Xavissa.Database.Models;

public class StockAdjustmentItem : IOfflineSyncEntity
{
    public int Id { get; set; }
    public Guid SyncId { get; set; } = Guid.NewGuid();
    public string? SourceDeviceId { get; set; }
    public DateTimeOffset? ClientCreatedAt { get; set; }
    public DateTimeOffset? ClientUpdatedAt { get; set; }
    public DateTimeOffset? LastSyncedAt { get; set; }
    public int StockAdjustmentId { get; set; }
    public StockAdjustment StockAdjustment { get; set; } = null!;
    public int VariantId { get; set; }
    public ProductVariant Variant { get; set; } = null!;
    public int OldQuantity { get; set; }
    public int NewQuantity { get; set; }
    public int DifferenceQuantity { get; set; }
    public string? Reason { get; set; }
    public string? Notes { get; set; }
}
