namespace Xavissa.Database.Models
{
    public class StandardProduct : IStoreScopedEntity, IAuditableEntity
    {
        public int Id { get; set; }
        public int StoreId { get; set; }
        public string Barcode { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public decimal DefaultPrice { get; set; }
        public DateTime? CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; } = DateTime.UtcNow;
        public int? CreatedBy { get; set; }
        public int? UpdatedBy { get; set; }
    }
}
