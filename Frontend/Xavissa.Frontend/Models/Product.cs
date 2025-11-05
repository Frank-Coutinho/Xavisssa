namespace Xavissa.Frontend.Models
{
    public enum ProductCategory
    {
        Camisas = 1,
        Calcas = 2,
        Sapatos = 3,
        Acessorios = 4,
        Other = 99,
    }

    public class Product
    {
        public int Id { get; set; }
        public string Code { get; set; } = string.Empty;
        public string Barcode { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;

        public string Color { get; set; } = "";
        public string ImageUrl { get; set; } = "";
        public string Description { get; set; } = string.Empty;
        public ProductCategory Category { get; set; } = ProductCategory.Other;
        public decimal Price { get; set; }
        public int StockQuantity { get; set; }
        public bool IsActive { get; set; } = true;
    }
}
