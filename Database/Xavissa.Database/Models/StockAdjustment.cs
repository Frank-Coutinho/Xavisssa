namespace Xavissa.Database.Models;

public class StockAdjustment : ITenantScopedEntity, IStoreScopedEntity, IAuditableEntity, IOfflineSyncEntity
{
    public int Id { get; set; }
    public Guid SyncId { get; set; } = Guid.NewGuid();
    public string? SourceDeviceId { get; set; }
    public DateTimeOffset? ClientCreatedAt { get; set; }
    public DateTimeOffset? ClientUpdatedAt { get; set; }
    public DateTimeOffset? LastSyncedAt { get; set; }
    public int? TenantId { get; set; }
    public int StoreId { get; set; }
    public string AdjustmentNumber { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
    public string Status { get; set; } = "Draft";
    public int? CreatedBy { get; set; }
    public int? ApprovedBy { get; set; }
    public int? AppliedBy { get; set; }
    public int? CancelledBy { get; set; }
    public DateTime? ApprovedAt { get; set; }
    public DateTime? AppliedAt { get; set; }
    public DateTime? CancelledAt { get; set; }
    public string? Notes { get; set; }
    public DateTime? CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
    public int? UpdatedBy { get; set; }

    public ICollection<StockAdjustmentItem> Items { get; set; } = new List<StockAdjustmentItem>();
}
