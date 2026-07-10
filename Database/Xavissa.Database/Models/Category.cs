namespace Xavissa.Database.Models;

public class Category : ITenantScopedEntity, IAuditableEntity, IOfflineSyncEntity
{
    public int Id { get; set; }
    public Guid SyncId { get; set; } = Guid.NewGuid();
    public string? SourceDeviceId { get; set; }
    public DateTimeOffset? ClientCreatedAt { get; set; }
    public DateTimeOffset? ClientUpdatedAt { get; set; }
    public DateTimeOffset? LastSyncedAt { get; set; }
    public int? TenantId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Code { get; set; }
    public int? ParentId { get; set; }
    public Category? Parent { get; set; }
    public List<Category> Children { get; set; } = new();
    public int Level { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime? CreatedAt { get; set; } = DateTime.UtcNow;
    public int? CreatedBy { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public DateTime? DeletedAt { get; set; }
    public int? UpdatedBy { get; set; }
    public List<Product> Products { get; set; } = new();
}
