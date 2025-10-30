namespace Xavissa.Database.Models
{

    public enum PaymentMethod
    {
        Cash = 0,
        Card= 1,
        MobilePayment = 2,
        Other= 99
    }
    public class Sale
    {
        public int Id { get; set; }
        public DateTime SaleDate { get; set; } = DateTime.UtcNow;
        public decimal TotalAmount { get; set; }
        public PaymentMethod PaymentMethod { get; set; } = PaymentMethod.Cash;
        public int? UserId { get; set; }
        public required string Code { get; set; }
        public User? User { get; set; }

        public decimal? Discount { get; set; }
        public decimal? AmountPaid { get; set; }
        public required string ReceiptNumber { get; set; }
        public bool IsRefunded { get; set; } = false;     // true if refunded
        public string? RefundReason { get; set; }

        // Sync metadata
        public DateTime LastModified { get; set; } = DateTime.UtcNow;

        // Relationships
        public ICollection<SaleItem> SaleItems { get; set; } = new List<SaleItem>();
    }
}
