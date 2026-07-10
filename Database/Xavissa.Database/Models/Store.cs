using System.ComponentModel.DataAnnotations;

namespace Xavissa.Database.Models
{
    public class Store : ITenantScopedEntity
    {
        [Key]
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Code { get; set; } = string.Empty;
        public DateTime? CreatedAt { get; set; } = DateTime.UtcNow;
        public int? TenantId { get; set; }
        public bool IsActive { get; set; } = true;
        public DateTime? UpdatedAt { get; set; }
        public int? UpdatedBy { get; set; }

        public Tenant? Tenant { get; set; }
        public ICollection<UserStoreRole> UserStores { get; set; } = new List<UserStoreRole>();
        public ICollection<StorePrintingSetting> PrintingSettings { get; set; } = new List<StorePrintingSetting>();
        public ICollection<ProductStoreAssignment> ProductAssignments { get; set; } = new List<ProductStoreAssignment>();
        public StoreOperationalSetting? OperationalSetting { get; set; }
    }
}
