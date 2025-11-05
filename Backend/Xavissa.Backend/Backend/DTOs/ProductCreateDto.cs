using Xavissa.Database.Models;

namespace Xavissa.Backend.DTOs
{
    public class ProductCreateDto
    {
        public string Name { get; set; }
        public string Description { get; set; }
        public ProductCategory Category { get; set; }
        public decimal Price { get; set; }
        public int StockQuantity { get; set; }
        public bool IsActive { get; set; } = true;

        public string Color { get; set; }
    }
}
