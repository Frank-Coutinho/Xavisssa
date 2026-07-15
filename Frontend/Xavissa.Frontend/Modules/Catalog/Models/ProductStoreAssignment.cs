using System;

namespace Xavissa.Frontend.Models
{
    public class ProductStoreAssignment
    {
        public int Id { get; set; }
        public int OnlineId { get; set; }
        public Guid SyncId { get; set; } = Guid.NewGuid();
        public string? SourceDeviceId { get; set; }
        public DateTimeOffset? ClientCreatedAt { get; set; }
        public DateTimeOffset? ClientUpdatedAt { get; set; }
        public DateTimeOffset? LastSyncedAt { get; set; }
        public int ProductId { get; set; }
        public int TenantId { get; set; }
        public int StoreId { get; set; }
        public string StoreName { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public int StockQuantity { get; set; }
        public bool IsActive { get; set; } = true;
        public DateTime? CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public DateTime? DeletedAt { get; set; }
        public int VariantCount { get; set; }
    }
}
