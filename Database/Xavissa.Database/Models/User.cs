using System.ComponentModel.DataAnnotations;

namespace Xavissa.Database.Models
{
    public enum UserRole
    {
        Superuser,
        Admin, // Store Owner
        Clerk,
    }

    public class User
    {
        [Key]
        public int Id { get; set; }
        public required string Code { get; set; }

        [Required]
        [MaxLength(50)]
        public string Username { get; set; }

        [Required]
        [MaxLength(255)]
        public string PasswordHash { get; set; } // Store hashed password

        [Required]
        [MaxLength(100)]
        public string Email { get; set; }

        [Required]
        public UserRole? Role { get; set; } = UserRole.Clerk;
    }
}
