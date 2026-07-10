using System.Collections.Generic;

namespace Xavissa.Frontend.Models
{
    public class UserReadDto
    {
        public int Id { get; set; }
        public string Username { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public bool IsActive { get; set; }
        public string PlatformRole { get; set; } = string.Empty;
        public string ActingRole { get; set; } = string.Empty;
        public string ClaimTypesRole { get; set; } = string.Empty;
        public List<string> AssignedStores { get; set; } = new();
        public List<ClaimReadDto> Claims { get; set; } = new();
    }

    public class ClaimReadDto
    {
        public int Id { get; set; }
        public string Type { get; set; } = string.Empty;
        public string Value { get; set; } = string.Empty;
    }
}
