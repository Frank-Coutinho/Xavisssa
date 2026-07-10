namespace Xavissa.Backend.DTOs
{
    public class LoginResponse
    {
        public string Token { get; set; } = string.Empty;
        public int UserId { get; set; }
        public string Username { get; set; } = string.Empty;
        public string PlatformRole { get; set; } = string.Empty;
        public int? PlatformRoleId { get; set; }
        public string? PlatformRoleCode { get; set; }
        public string? ActingRole { get; set; }
        public int? SelectedTenantId { get; set; }
        public int? SelectedStoreId { get; set; }
        public List<LoginTenantDto> AllowedTenants { get; set; } = new();
        public List<LoginStoreDto> AllowedStores { get; set; } = new();
    }

    public class LoginTenantDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public int? TenantRoleId { get; set; }
        public string TenantRoleCode { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
    }

    public class LoginStoreDto
    {
        public int Id { get; set; }
        public int TenantId { get; set; }
        public string Name { get; set; } = string.Empty;
        public int? StoreRoleId { get; set; }
        public string StoreRoleCode { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
    }
}
