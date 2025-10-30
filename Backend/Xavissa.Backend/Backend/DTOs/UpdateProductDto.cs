using Xavissa.Database.Models;
namespace Xavissa.Backend.DTOs
{
    public class UpdateProductDto
    {
        public string Description { get; set; }
        public decimal Price { get; set; }
        public int StockQuantity { get; set; }
        public bool IsActive { get; set; } = true;
    }
}