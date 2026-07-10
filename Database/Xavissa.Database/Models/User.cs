using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Xavissa.Database.Models
{
    public class User
    {
        [Key]
        public int Id { get; set; }
        public string Code { get; set; } = string.Empty;

        [Required]
        [MaxLength(50)]
        public string Username { get; set; } = string.Empty;

        [Required]
        [MaxLength(255)]
        public string PasswordHash { get; set; } = string.Empty;

        [Required]
        [MaxLength(100)]
        public string Email { get; set; } = string.Empty;
        public int? PlatformRoleId { get; set; }
        public Role? PlatformRoleNavigation { get; set; }
        [NotMapped]
        public string? PlatformRole { get; set; }
        public bool IsActive { get; set; } = true;
        public DateTime? LastLoginAt { get; set; }
        public DateTime? CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }
        public ICollection<TenantUser> TenantUsers { get; set; } = new List<TenantUser>();
        public ICollection<UserStoreRole> UserStores { get; set; } = new List<UserStoreRole>();
    }
}
