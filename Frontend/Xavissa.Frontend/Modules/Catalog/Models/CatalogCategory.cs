using System;

namespace Xavissa.Frontend.Models
{
    public class CatalogCategory
    {
        public int Id { get; set; }
        public int OnlineId { get; set; }
        public Guid SyncId { get; set; } = Guid.NewGuid();
        public string? SourceDeviceId { get; set; }
        public DateTimeOffset? ClientCreatedAt { get; set; }
        public DateTimeOffset? ClientUpdatedAt { get; set; }
        public DateTimeOffset? LastSyncedAt { get; set; }
        public int TenantId { get; set; }
        public string Name { get; set; } = string.Empty;
        public bool IsActive { get; set; }
        public DateTime? CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public DateTime? DeletedAt { get; set; }
        public int ProductCount { get; set; }

        public override string ToString() => Name;
    }
}
