namespace Xavissa.Database.Models
{
    public class DeletedSale
    {
        public int Id { get; set; }
        public int OriginalSaleId { get; set; }
        public string Reason { get; set; } = string.Empty;
        public DateTime DeletedAt { get; set; } = DateTime.UtcNow;

        // Optional for auditing
        public int? UserId { get; set; }
        public User? User { get; set; }
    }
}
