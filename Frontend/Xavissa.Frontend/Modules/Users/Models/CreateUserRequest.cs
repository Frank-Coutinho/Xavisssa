namespace Xavissa.Frontend.Models
{
    public class CreateUserRequest
    {
        public string Username { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public int? PlatformRoleId { get; set; }
        public string PlatformRoleCode { get; set; } = string.Empty;
        public string PlatformRole { get; set; } = string.Empty;
        public int? AssignedRoleId { get; set; }
        public string AssignedRoleCode { get; set; } = string.Empty;
        public string AssignedRole { get; set; } = string.Empty;
        public int? TenantId { get; set; }
        public int? StoreId { get; set; }
    }
}
