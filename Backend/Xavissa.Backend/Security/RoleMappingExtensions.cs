namespace Xavissa.Backend.Security
{
    public static class RoleMappingExtensions
    {
        public static bool IsPlatformAdmin(this string? platformRole)
        {
            return string.Equals(platformRole, AccessRoles.SystemAdmin, StringComparison.OrdinalIgnoreCase)
                || string.Equals(platformRole, "SystemAdmin", StringComparison.OrdinalIgnoreCase)
                || string.Equals(platformRole, "SuperAdmin", StringComparison.OrdinalIgnoreCase);
        }

        public static bool IsSupport(this string? platformRole)
        {
            return string.Equals(platformRole, AccessRoles.Support, StringComparison.OrdinalIgnoreCase)
                || string.Equals(platformRole, "Support", StringComparison.OrdinalIgnoreCase);
        }

        public static bool IsTenantAdmin(this string? role)
        {
            return string.Equals(role, AccessRoles.TenantAdmin, StringComparison.OrdinalIgnoreCase)
                || string.Equals(role, "TenantAdmin", StringComparison.OrdinalIgnoreCase);
        }

        public static bool IsStoreManager(this string? role)
        {
            return string.Equals(role, AccessRoles.StoreManager, StringComparison.OrdinalIgnoreCase)
                || string.Equals(role, "StoreManager", StringComparison.OrdinalIgnoreCase)
                || string.Equals(role, "Manager", StringComparison.OrdinalIgnoreCase);
        }

        public static bool IsClerkLike(this string? role)
        {
            return string.Equals(role, AccessRoles.Clerk, StringComparison.OrdinalIgnoreCase)
                || string.Equals(role, "Clerk", StringComparison.OrdinalIgnoreCase)
                || string.Equals(role, "Cashier", StringComparison.OrdinalIgnoreCase);
        }

        public static string NormalizeRoleCode(this string? role)
        {
            if (role.IsPlatformAdmin())
                return AccessRoles.SystemAdmin;
            if (role.IsSupport())
                return AccessRoles.Support;
            if (role.IsTenantAdmin())
                return AccessRoles.TenantAdmin;
            if (role.IsStoreManager())
                return AccessRoles.StoreManager;
            if (role.IsClerkLike())
                return AccessRoles.Clerk;
            return string.IsNullOrWhiteSpace(role) ? string.Empty : role.Trim().ToUpperInvariant();
        }
    }
}

