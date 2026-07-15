using System;

namespace Xavissa.Frontend.Data.Entities
{
    public class OfflineIdentity
    {
        public int Id { get; set; }
        public int OnlineUserId { get; set; }
        public string Username { get; set; } = string.Empty;
        public string PasswordHash { get; set; } = string.Empty;
        public string ApiToken { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
        public int? PlatformRoleId { get; set; }
        public string PlatformRoleCode { get; set; } = string.Empty;
        public string PlatformRole { get; set; } = string.Empty;
        public string ActingRole { get; set; } = string.Empty;
        public string AllowedTenantsJson { get; set; } = "[]";
        public string AllowedStoresJson { get; set; } = "[]";
        public int? SelectedTenantId { get; set; }
        public int? SelectedStoreId { get; set; }
        public DateTime LastOnlineLogin { get; set; }
        public bool IsActive { get; set; } = true;
    }
}
