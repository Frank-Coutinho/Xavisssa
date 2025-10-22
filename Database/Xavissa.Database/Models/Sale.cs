namespace Xavissa.Database.Models
{
    public class Sale
    {
        public int Id { get; set; }
        public DateTime SaleDate { get; set; } = DateTime.UtcNow;
        public decimal TotalAmount { get; set; }
        public string PaymentMethod { get; set; } = "Cash";
        public int? UserId { get; set; }
        public User? User { get; set; }

        // Sync metadata
        public DateTime LastModified { get; set; } = DateTime.UtcNow;

        // Relationships
        public ICollection<SaleItem> SaleItems { get; set; } = new List<SaleItem>();
    }
}
