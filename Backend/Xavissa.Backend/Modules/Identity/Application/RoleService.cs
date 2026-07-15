using Microsoft.EntityFrameworkCore;
using Xavissa.Backend.DTOs;
using Xavissa.Backend.Security;
using Xavissa.Database;
using Xavissa.Database.Models;

namespace Xavissa.Backend.Services;

public class RoleService : IRoleService
{
    private readonly XavissaDbContext _db;
    private readonly ILogger<RoleService> _logger;

    public RoleService(XavissaDbContext db, ILogger<RoleService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<List<RoleDto>> GetRolesAsync()
    {
        return await _db.Roles
            .AsNoTracking()
            .OrderBy(x => x.Scope)
            .ThenBy(x => x.Name)
            .Select(x => new RoleDto
            {
                Id = x.Id,
                Code = x.Code,
                Name = x.Name,
                Scope = x.Scope,
                Description = x.Description,
                IsSystemRole = x.IsSystemRole,
                IsActive = x.IsActive,
            })
            .ToListAsync();
    }

    public async Task<List<RoleDto>> GetRolesByScopeAsync(string scope)
    {
        return await _db.Roles
            .AsNoTracking()
            .Where(x => x.IsActive && x.Scope == scope)
            .OrderBy(x => x.Name)
            .Select(x => new RoleDto
            {
                Id = x.Id,
                Code = x.Code,
                Name = x.Name,
                Scope = x.Scope,
                Description = x.Description,
                IsSystemRole = x.IsSystemRole,
                IsActive = x.IsActive,
            })
            .ToListAsync();
    }

    public async Task<RoleDto?> GetRoleByCodeAsync(string code)
    {
        var normalizedCode = NormalizeRoleCode(code);
        return await _db.Roles
            .AsNoTracking()
            .Where(x => x.Code == normalizedCode)
            .Select(x => new RoleDto
            {
                Id = x.Id,
                Code = x.Code,
                Name = x.Name,
                Scope = x.Scope,
                Description = x.Description,
                IsSystemRole = x.IsSystemRole,
                IsActive = x.IsActive,
            })
            .FirstOrDefaultAsync();
    }

    public async Task<bool> ValidateRoleScopeAsync(int roleId, string expectedScope)
    {
        return await _db.Roles
            .AsNoTracking()
            .AnyAsync(x => x.Id == roleId && x.IsActive && x.Scope == expectedScope);
    }

    public async Task<int> ResolveRoleIdAsync(string roleCode, string expectedScope)
    {
        var normalizedCode = NormalizeRoleCode(roleCode);
        var role = await _db.Roles
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Code == normalizedCode && x.IsActive);

        if (role == null)
            throw new InvalidOperationException($"Role '{normalizedCode}' was not found.");

        if (!string.Equals(role.Scope, expectedScope, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException($"Role '{normalizedCode}' is not a {expectedScope} role.");

        return role.Id;
    }

    public async Task<string?> ResolveRoleCodeAsync(int? roleId, string? legacyRole, string expectedScope)
    {
        if (roleId.HasValue)
        {
            var role = await _db.Roles
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == roleId.Value && x.IsActive);
            if (role != null)
            {
                if (!string.Equals(role.Scope, expectedScope, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException($"Role '{role.Code}' is not a {expectedScope} role.");
                return role.Code;
            }
        }

        if (!string.IsNullOrWhiteSpace(legacyRole))
        {
            var normalized = NormalizeRoleCode(legacyRole);
            _logger.LogWarning(
                "Using legacy role text fallback '{LegacyRole}' normalized to '{RoleCode}'.",
                legacyRole,
                normalized);
            return normalized;
        }

        return null;
    }

    private static string NormalizeRoleCode(string roleCode) => roleCode.NormalizeRoleCode();
}
