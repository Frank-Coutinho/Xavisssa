using Microsoft.EntityFrameworkCore;
using Xavissa.Backend.Security;
using Xavissa.Backend.Utilities;
using Xavissa.Database;
using Xavissa.Database.Models;

namespace Xavissa.Backend.Services;

public class SystemAdminSeedService
{
    private readonly XavissaDbContext _db;

    public SystemAdminSeedService(XavissaDbContext db)
    {
        _db = db;
    }

    public async Task<string> SeedAsync(string username, string email, string password, bool overwrite = false)
    {
        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
            throw new ArgumentException("Username, email, and password are required.");

        var systemAdminRole = await EnsureRoleAsync(AccessRoles.SystemAdmin, RoleScopes.Platform);
        await EnsureRoleAsync(AccessRoles.Support, RoleScopes.Platform);
        await EnsureRoleAsync(AccessRoles.User.ToUpperInvariant(), RoleScopes.Platform);
        await EnsureRoleAsync(AccessRoles.TenantAdmin, RoleScopes.Tenant);
        await EnsureRoleAsync(AccessRoles.StoreManager, RoleScopes.Store);
        await EnsureRoleAsync(AccessRoles.Clerk, RoleScopes.Store);
        await _db.SaveChangesAsync();

        var existingSystemAdmin = await _db.Users
            .IgnoreQueryFilters()
            .AnyAsync(x => x.PlatformRoleId == systemAdminRole.Id);
        if (existingSystemAdmin && !overwrite)
            return "System admin already exists. No changes made.";

        var normalizedUsername = username.Trim();
        var user = await _db.Users.IgnoreQueryFilters().FirstOrDefaultAsync(x => x.Username == normalizedUsername);
        if (user == null)
        {
            user = new User
            {
                Username = normalizedUsername,
                Email = email.Trim(),
                PasswordHash = PasswordHasher.HashPassword(password),
                PlatformRoleId = systemAdminRole.Id,
                Code = IdGenerator.GenerateId("SYS"),
                IsActive = true,
            };
            _db.Users.Add(user);
            await _db.SaveChangesAsync();
            return "System admin created.";
        }

        if (!overwrite && user.PlatformRoleId != systemAdminRole.Id)
            throw new InvalidOperationException("Username already exists and is not the system admin.");

        user.Email = email.Trim();
        user.PlatformRoleId = systemAdminRole.Id;
        user.IsActive = true;
        if (overwrite)
            user.PasswordHash = PasswordHasher.HashPassword(password);
        await _db.SaveChangesAsync();
        return overwrite ? "System admin updated." : "System admin already exists. No changes made.";
    }

    private async Task<Role> EnsureRoleAsync(string code, string scope)
    {
        var normalizedCode = code.Trim().ToUpperInvariant();
        var role = await _db.Roles.IgnoreQueryFilters().FirstOrDefaultAsync(x => x.Code == normalizedCode && x.Scope == scope);
        if (role != null)
            return role;

        role = new Role
        {
            Code = normalizedCode,
            Name = normalizedCode.Replace('_', ' '),
            Scope = scope,
            IsSystemRole = true,
            IsActive = true,
        };
        _db.Roles.Add(role);
        return role;
    }
}
