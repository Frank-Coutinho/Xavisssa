
using Xavissa.Database.Models;

namespace Xavissa.Backend.DTOs
{
    public class UpdateUserRequest
    {
        public string? Username { get; set; }
        public string? Email { get; set; }
        public UserRole? Role { get; set; }
    }
}
