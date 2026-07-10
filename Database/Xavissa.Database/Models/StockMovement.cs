namespace Xavissa.Database.Models;

public class StockMovement : ITenantScopedEntity, IStoreScopedEntity, IOfflineSyncEntity
{
    public int Id { get; set; }
    public Guid SyncId { get; set; } = Guid.NewGuid();
    public string? SourceDeviceId { get; set; }
    public DateTimeOffset? ClientCreatedAt { get; set; }
    public DateTimeOffset? ClientUpdatedAt { get; set; }
    public DateTimeOffset? LastSyncedAt { get; set; }
    public int VariantId { get; set; }
    public ProductVariant Variant { get; set; } = null!;
    public int Quantity { get; set; }
    public string MovementType { get; set; } = string.Empty;
    public int? ReferenceId { get; set; }
    public DateTime? CreatedAt { get; set; } = DateTime.UtcNow;
    public int StoreId { get; set; }
    public int? CreatedBy { get; set; }
    public int? TenantId { get; set; }
    public string? ReferenceType { get; set; }
    public string? Notes { get; set; }
}
