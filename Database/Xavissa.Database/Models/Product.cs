using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;

namespace Xavissa.Database.Models;

public class Product : ITenantScopedEntity, IAuditableEntity, IOfflineSyncEntity
{
    public int Id { get; set; }
    public Guid SyncId { get; set; } = Guid.NewGuid();
    public string? SourceDeviceId { get; set; }
    public DateTimeOffset? ClientCreatedAt { get; set; }
    public DateTimeOffset? ClientUpdatedAt { get; set; }
    public DateTimeOffset? LastSyncedAt { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime? UpdatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? DeletedAt { get; set; }
    public string Code { get; set; } = string.Empty;
    public DateTime? CreatedAt { get; set; } = DateTime.UtcNow;
    public int? CreatedBy { get; set; }
    public int? UpdatedBy { get; set; }
    public int? TenantId { get; set; }
    public int? CategoryId { get; set; }
    public Category? CategoryNavigation { get; set; }
    public string? Brand { get; set; }

    public ICollection<ProductStoreAssignment> StoreAssignments { get; set; } = new List<ProductStoreAssignment>();

    [NotMapped]
    public ICollection<ProductVariant> Variants =>
        StoreAssignments.SelectMany(assignment => assignment.Variants).ToList();

    [NotMapped]
    public string CategoryName => CategoryNavigation?.Name ?? string.Empty;

    [NotMapped]
    public ProductVariant? PrimaryVariant =>
        Variants.FirstOrDefault(v => v.IsActive != false) ?? Variants.FirstOrDefault();

    [NotMapped]
    public string Barcode => PrimaryVariant?.Barcode ?? string.Empty;

    [NotMapped]
    public decimal Price => PrimaryVariant?.Price ?? 0;
}
