namespace Xavissa.Backend.DTOs
{
    public class ProductStoreAssignmentDto
    {
        public int Id { get; set; }
        public int ProductId { get; set; }
        public int TenantId { get; set; }
        public int StoreId { get; set; }
        public string StoreName { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public int StockQuantity { get; set; }
        public bool IsActive { get; set; }
        public int VariantCount { get; set; }
    }

    public class SaveProductStoreAssignmentDto
    {
        public int TenantId { get; set; }
        public int StoreId { get; set; }
        public bool IsActive { get; set; } = true;
    }
}
