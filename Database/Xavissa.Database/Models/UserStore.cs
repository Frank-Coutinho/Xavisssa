using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Xavissa.Database.Models
{
    [Table("UserStoreRoles")]
    public class UserStoreRole : ITenantScopedEntity, IAuditableEntity
    {
        [Key]
        public int Id { get; set; }

        public int UserId { get; set; }
        public User User { get; set; } = null!;

        public int StoreId { get; set; }
        public Store Store { get; set; } = null!;

        public int? RoleId { get; set; }
        public Role? RoleNavigation { get; set; }
        [NotMapped]
        public string? Role { get; set; }

        public bool IsActive { get; set; } = true;
        public DateTime? CreatedAt { get; set; } = DateTime.UtcNow;
        public int? CreatedBy { get; set; }
        public int? TenantId { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public int? UpdatedBy { get; set; }
    }
}
