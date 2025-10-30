using Xavissa.Database.Models;

namespace Xavissa.Backend.DTOs
{
    public class SaleCreateDto
    {
        public required List<SaleItemDto> SaleItems { get; set; }
    }

    public class SaleItemDto
    {
        public int ProductId { get; set; }
        public int Quantity { get; set; }
        public bool IsRefunded { get; set; } = false;
        public string? RefundReason { get; set; }
        public PaymentMethod PaymentMethod { get; set; }
    }
}
