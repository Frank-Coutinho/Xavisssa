namespace Xavissa.Database.Models;
public class DeletedSale
{
    public int Id { get; set; }
    public int OriginalSaleId { get; set; }
    public DateTime SaleDate { get; set; }
    public decimal TotalAmount { get; set; }
    public decimal Discount { get; set; }
    public decimal AmountPaid { get; set; }
    public string PaymentMethod { get; set; } = string.Empty;
    public string ReceiptNumber { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public int UserId { get; set; }

    public DateTime DeletedAt { get; set; } = DateTime.UtcNow;
    public int? DeletedByUserId { get; set; }

    public List<DeletedSaleItem> SaleItems { get; set; } = new();
}
