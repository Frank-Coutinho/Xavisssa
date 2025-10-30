namespace Xavissa.Database.Models
{

    public enum ProductCategory
    {
        Camisas = 1,
        calcas = 2,
        Sapatos = 3,
        Acessorios = 4,
        Other = 99
    }
    public class Product
    {
        public int Id { get; set; }
        public required string Code { get; set; }
        public string Barcode { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public ProductCategory Category { get; set; } = ProductCategory.Other;
        public decimal Price { get; set; }
        public int StockQuantity { get; set; }
        public bool IsActive { get; set; } = true;

        // Optional: for online-offline sync
        public DateTime LastModified { get; set; } = DateTime.UtcNow;

        // Relationships
        public ICollection<SaleItem> SaleItems { get; set; } = new List<SaleItem>();
    }
}
