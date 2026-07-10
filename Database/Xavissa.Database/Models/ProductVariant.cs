using System.ComponentModel.DataAnnotations.Schema;

namespace Xavissa.Database.Models;

public class ProductVariant : ITenantScopedEntity, IStoreScopedEntity, IAuditableEntity, IOfflineSyncEntity
{
    private int? _storeIdOverride;

    public int Id { get; set; }
    public Guid SyncId { get; set; } = Guid.NewGuid();
    public string? SourceDeviceId { get; set; }
    public DateTimeOffset? ClientCreatedAt { get; set; }
    public DateTimeOffset? ClientUpdatedAt { get; set; }
    public DateTimeOffset? LastSyncedAt { get; set; }
    [NotMapped]
    public int ProductId => ProductStoreAssignment?.ProductId ?? 0;
    [NotMapped]
    public Product? Product => ProductStoreAssignment?.Product;
    [Column("StoreProductId")]
    public int ProductStoreAssignmentId { get; set; }
    public ProductStoreAssignment? ProductStoreAssignment { get; set; }
    [NotMapped]
    public int StoreId
    {
        get => ProductStoreAssignment?.StoreId ?? _storeIdOverride ?? 0;
        set => _storeIdOverride = value > 0 ? value : null;
    }
    public string? SKU { get; set; }
    public string? Barcode { get; set; }
    public decimal? Price { get; set; }
    [NotMapped]
    public decimal? CostPrice { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime? CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? DeletedAt { get; set; }
    public int? CreatedBy { get; set; }
    public int? UpdatedBy { get; set; }
    public int? TenantId { get; set; }
    public string? Description { get; set; }
    public string? Label { get; set; }
    public string? AttributesJson { get; set; }

    public ICollection<SaleItem> SaleItems { get; set; } = new List<SaleItem>();
    public ICollection<StockMovement> StockMovements { get; set; } = new List<StockMovement>();
    public ICollection<StockLevel> StockLevels { get; set; } = new List<StockLevel>();
}
