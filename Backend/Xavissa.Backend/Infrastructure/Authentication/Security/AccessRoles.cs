namespace Xavissa.Backend.Security
{
    public static class AccessRoles
    {
        public const string SystemAdmin = "SYSTEM_ADMIN";
        public const string Support = "SUPPORT";
        public const string TenantAdmin = "TENANT_ADMIN";
        public const string StoreManager = "STORE_MANAGER";
        public const string Clerk = "CLERK";
        public const string Cashier = Clerk;
        public const string User = "User";

        public const string SuperAdmin = SystemAdmin;
        public const string Manager = StoreManager;
    }

    public static class RoleScopes
    {
        public const string Platform = "Platform";
        public const string Tenant = "Tenant";
        public const string Store = "Store";
    }
}
