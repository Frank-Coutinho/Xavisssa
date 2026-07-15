namespace Xavissa.Backend.DTOs
{
    public class ProductReadDto
    {
        public int Id { get; set; }
        public int OnlineId { get; set; }
        public Guid SyncId { get; set; }
        public string? SourceDeviceId { get; set; }
        public DateTimeOffset? ClientCreatedAt { get; set; }
        public DateTimeOffset? ClientUpdatedAt { get; set; }
        public DateTimeOffset? LastSyncedAt { get; set; }
        public int TenantId { get; set; }
        public int StoreId { get; set; }
        public int AssignmentId { get; set; }
        public int VariantId { get; set; }
        public int? CategoryId { get; set; }
        public string Code { get; set; } = string.Empty;
        public string Barcode { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string SKU { get; set; } = string.Empty;
        public string? Label { get; set; }
        public string? Description { get; set; }
        public string Category { get; set; } = string.Empty;
        public string? Brand { get; set; }
        public decimal Price { get; set; }
        public int StockQuantity { get; set; }
        public bool IsActive { get; set; }
        public int VariantCount { get; set; }
        public DateTime? CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public DateTime? DeletedAt { get; set; }
    }
}
