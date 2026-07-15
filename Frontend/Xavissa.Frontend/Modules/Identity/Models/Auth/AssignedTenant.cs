using System;

namespace Xavissa.Frontend.Models.Auth
{
    public class AssignedTenant
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public int? TenantRoleId { get; set; }
        public string TenantRoleCode { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;

        public string EffectiveRole => string.IsNullOrWhiteSpace(TenantRoleCode) ? Role : TenantRoleCode;
        public bool IsTenantAdmin => AppRoles.IsTenantAdmin(EffectiveRole);
    }
}
