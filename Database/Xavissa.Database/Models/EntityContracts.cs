namespace Xavissa.Database.Models
{
    public interface ITenantScopedEntity
    {
        int? TenantId { get; set; }
    }

    public interface IStoreScopedEntity
    {
        int StoreId { get; set; }
    }

    public interface IAuditableEntity
    {
        DateTime? CreatedAt { get; set; }
        DateTime? UpdatedAt { get; set; }
        int? CreatedBy { get; set; }
        int? UpdatedBy { get; set; }
    }

    public interface IOfflineSyncEntity
    {
        Guid SyncId { get; set; }
        string? SourceDeviceId { get; set; }
        DateTimeOffset? ClientCreatedAt { get; set; }
        DateTimeOffset? ClientUpdatedAt { get; set; }
        DateTimeOffset? LastSyncedAt { get; set; }
    }
}
