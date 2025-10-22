using System.ComponentModel.DataAnnotations;

namespace Xavissa.Database.Models
{
    public class User
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [MaxLength(50)]
        public string Username { get; set; }

        [Required]
        [MaxLength(255)]
        public string PasswordHash { get; set; } // Store hashed password

        [Required]
        [MaxLength(100)]
        public string Email { get; set; }
    }
}
