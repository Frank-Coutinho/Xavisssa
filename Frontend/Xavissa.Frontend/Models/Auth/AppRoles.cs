using System;

namespace Xavissa.Frontend.Models.Auth
{
    public static class AppRoles
    {
        public const string SystemAdmin = "SYSTEM_ADMIN";
        public const string TenantAdmin = "TENANT_ADMIN";
        public const string StoreManager = "STORE_MANAGER";
        public const string Clerk = "CLERK";
        public const string Cashier = Clerk;
        public const string Support = "SUPPORT";
        public const string User = "User";

        public static bool IsSystemAdmin(string? role) =>
            string.Equals(role, SystemAdmin, StringComparison.OrdinalIgnoreCase)
            || string.Equals(role, "SystemAdmin", StringComparison.OrdinalIgnoreCase)
            || string.Equals(role, "SuperAdmin", StringComparison.OrdinalIgnoreCase);

        public static bool IsSupport(string? role) =>
            string.Equals(role, Support, StringComparison.OrdinalIgnoreCase)
            || string.Equals(role, "Support", StringComparison.OrdinalIgnoreCase);

        public static bool IsTenantAdmin(string? role) =>
            string.Equals(role, TenantAdmin, StringComparison.OrdinalIgnoreCase)
            || string.Equals(role, "TenantAdmin", StringComparison.OrdinalIgnoreCase);

        public static bool IsStoreManager(string? role) =>
            string.Equals(role, StoreManager, StringComparison.OrdinalIgnoreCase)
            || string.Equals(role, "StoreManager", StringComparison.OrdinalIgnoreCase)
            || string.Equals(role, "Manager", StringComparison.OrdinalIgnoreCase);

        public static bool IsClerkLike(string? role) =>
            string.Equals(role, Clerk, StringComparison.OrdinalIgnoreCase)
            || string.Equals(role, "Clerk", StringComparison.OrdinalIgnoreCase)
            || string.Equals(role, "Cashier", StringComparison.OrdinalIgnoreCase);

        public static string NormalizeRoleCode(string? role)
        {
            if (IsSystemAdmin(role))
                return SystemAdmin;
            if (IsSupport(role))
                return Support;
            if (IsTenantAdmin(role))
                return TenantAdmin;
            if (IsStoreManager(role))
                return StoreManager;
            if (IsClerkLike(role))
                return Clerk;
            return string.IsNullOrWhiteSpace(role) ? string.Empty : role.Trim().ToUpperInvariant();
        }
    }
}
