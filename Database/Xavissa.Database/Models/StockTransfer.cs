namespace Xavissa.Database.Models;

public class StockTransfer : ITenantScopedEntity, IAuditableEntity, IOfflineSyncEntity
{
    public int Id { get; set; }
    public Guid SyncId { get; set; } = Guid.NewGuid();
    public string? SourceDeviceId { get; set; }
    public DateTimeOffset? ClientCreatedAt { get; set; }
    public DateTimeOffset? ClientUpdatedAt { get; set; }
    public DateTimeOffset? LastSyncedAt { get; set; }
    public int? TenantId { get; set; }
    public string TransferNumber { get; set; } = string.Empty;
    public int FromStoreId { get; set; }
    public int ToStoreId { get; set; }
    public string Status { get; set; } = "Draft";
    public int RequestedBy { get; set; }
    public int? ApprovedBy { get; set; }
    public int? SentBy { get; set; }
    public int? ReceivedBy { get; set; }
    public int? CancelledBy { get; set; }
    public DateTime RequestedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ApprovedAt { get; set; }
    public DateTime? SentAt { get; set; }
    public DateTime? ReceivedAt { get; set; }
    public DateTime? CancelledAt { get; set; }
    public string? Notes { get; set; }
    public DateTime? CreatedAt { get; set; } = DateTime.UtcNow;
    public int? CreatedBy { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public int? UpdatedBy { get; set; }

    public ICollection<StockTransferItem> Items { get; set; } = new List<StockTransferItem>();
}
