using System;

namespace Xavissa.Frontend.Models
{
    public class ProductVariantRecord
    {
        public int Id { get; set; }
        public int OnlineId { get; set; }
        public Guid SyncId { get; set; } = Guid.NewGuid();
        public string? SourceDeviceId { get; set; }
        public DateTimeOffset? ClientCreatedAt { get; set; }
        public DateTimeOffset? ClientUpdatedAt { get; set; }
        public DateTimeOffset? LastSyncedAt { get; set; }
        public int ProductId { get; set; }
        public int AssignmentId { get; set; }
        public int StoreProductId { get => AssignmentId; set => AssignmentId = value; }
        public int TenantId { get; set; }
        public int StoreId { get; set; }
        public string StoreName { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string Label { get; set; } = string.Empty;
        public string SKU { get; set; } = string.Empty;
        public string Barcode { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public decimal? CostPrice { get; set; }
        public int StockQuantity { get; set; }
        public bool IsActive { get; set; } = true;
        public string? AttributesJson { get; set; }
        public DateTime? CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public DateTime? DeletedAt { get; set; }

        public string DisplayName => string.IsNullOrWhiteSpace(Label) ? Name : $"{Name} - {Label}";
        public string StatusLabel => IsActive ? "Active" : "Inactive";
    }
}
