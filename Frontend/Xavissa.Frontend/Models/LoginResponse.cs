using System.Collections.Generic;

namespace Xavissa.Frontend.Models.Auth
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
        public List<AssignedTenant> AllowedTenants { get; set; } = new();
        public List<AssignedStore> AllowedStores { get; set; } = new();
        public List<Claim> Claims { get; set; } = new();
    }
}
