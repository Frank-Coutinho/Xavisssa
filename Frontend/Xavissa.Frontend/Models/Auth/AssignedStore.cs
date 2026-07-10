using System;

namespace Xavissa.Frontend.Models.Auth
{
    public class AssignedStore
    {
        public int Id { get; set; }
        public int TenantId { get; set; }
        public string Name { get; set; } = string.Empty;
        public int? StoreRoleId { get; set; }
        public string StoreRoleCode { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;

        public string EffectiveRole => string.IsNullOrWhiteSpace(StoreRoleCode) ? Role : StoreRoleCode;
        public bool IsManager => AppRoles.IsStoreManager(EffectiveRole);
        public bool IsClerkLike => AppRoles.IsClerkLike(EffectiveRole);
    }
}
