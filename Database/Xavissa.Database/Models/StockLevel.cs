namespace Xavissa.Database.Models;

public class StockLevel : ITenantScopedEntity, IStoreScopedEntity, IAuditableEntity, IOfflineSyncEntity
{
    public int Id { get; set; }
    public Guid SyncId { get; set; } = Guid.NewGuid();
    public string? SourceDeviceId { get; set; }
    public DateTimeOffset? ClientCreatedAt { get; set; }
    public DateTimeOffset? ClientUpdatedAt { get; set; }
    public DateTimeOffset? LastSyncedAt { get; set; }
    public int? TenantId { get; set; }
    public int StoreId { get; set; }
    public int VariantId { get; set; }
    public ProductVariant Variant { get; set; } = null!;
    public int QuantityOnHand { get; set; }
    public int? ReorderLevel { get; set; }
    public DateTime? UpdatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CreatedAt { get; set; }
    public int? CreatedBy { get; set; }
    public int? UpdatedBy { get; set; }
}
