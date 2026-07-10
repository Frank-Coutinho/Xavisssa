namespace Xavissa.Database.Models
{
    public class SaleItem : ITenantScopedEntity, IStoreScopedEntity, IAuditableEntity, IOfflineSyncEntity
    {
        public int Id { get; set; }
        public Guid SyncId { get; set; } = Guid.NewGuid();
        public string? SourceDeviceId { get; set; }
        public DateTimeOffset? ClientCreatedAt { get; set; }
        public DateTimeOffset? ClientUpdatedAt { get; set; }
        public DateTimeOffset? LastSyncedAt { get; set; }
        public int SaleId { get; set; }
        public Sale Sale { get; set; } = null!;
        public int Quantity { get; set; }
        public required decimal UnitPrice { get; set; }
        public DateTime? UpdatedAt { get; set; } = DateTime.UtcNow;
        public int VariantId { get; set; }
        public ProductVariant Variant { get; set; } = null!;
        public bool IsRefunded { get; set; }
        public int RefundedQuantity { get; set; }
        public string? RefundReason { get; set; }
        public DateTime? RefundedAt { get; set; }
        public int? RefundedByUserId { get; set; }
        public int StoreId { get; set; }
        public int? CreatedBy { get; set; }
        public int? UpdatedBy { get; set; }
        public int? TenantId { get; set; }
        public DateTime? CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
