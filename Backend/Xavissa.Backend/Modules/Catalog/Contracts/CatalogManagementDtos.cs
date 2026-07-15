namespace Xavissa.Backend.DTOs
{
    public class CategoryReadDto
    {
        public int Id { get; set; }
        public int OnlineId { get; set; }
        public Guid SyncId { get; set; }
        public int TenantId { get; set; }
        public string? SourceDeviceId { get; set; }
        public DateTimeOffset? ClientCreatedAt { get; set; }
        public DateTimeOffset? ClientUpdatedAt { get; set; }
        public DateTimeOffset? LastSyncedAt { get; set; }
        public string Name { get; set; } = string.Empty;
        public bool IsActive { get; set; }
        public int ProductCount { get; set; }
    }

    public class SaveCategoryDto
    {
        public int? TenantId { get; set; }
        public string Name { get; set; } = string.Empty;
        public bool IsActive { get; set; } = true;
    }

    public class ProductVariantReadDto
    {
        public int Id { get; set; }
        public int OnlineId { get; set; }
        public Guid SyncId { get; set; }
        public int ProductId { get; set; }
        public int AssignmentId { get; set; }
        public int StoreProductId { get => AssignmentId; set => AssignmentId = value; }
        public int TenantId { get; set; }
        public int StoreId { get; set; }
        public string? SourceDeviceId { get; set; }
        public DateTimeOffset? ClientCreatedAt { get; set; }
        public DateTimeOffset? ClientUpdatedAt { get; set; }
        public DateTimeOffset? LastSyncedAt { get; set; }
        public string StoreName { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string? Label { get; set; }
        public string? SKU { get; set; }
        public string Barcode { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public decimal? CostPrice { get; set; }
        public int StockQuantity { get; set; }
        public bool IsActive { get; set; }
    }

    public class SaveProductVariantDto
    {
        public int? AssignmentId { get; set; }
        public int? StoreId { get; set; }
        public string? Description { get; set; }
        public string? Label { get; set; }
        public string? SKU { get; set; }
        public string? Barcode { get; set; }
        public decimal Price { get; set; }
        public decimal? CostPrice { get; set; }
        public int StockQuantity { get; set; }
        public bool IsActive { get; set; } = true;
        public bool GenerateBarcode { get; set; }
    }
}
