using Xavissa.Backend.DTOs;

namespace Xavissa.Backend.Services;

public interface IRoleService
{
    Task<List<RoleDto>> GetRolesAsync();
    Task<List<RoleDto>> GetRolesByScopeAsync(string scope);
    Task<RoleDto?> GetRoleByCodeAsync(string code);
    Task<bool> ValidateRoleScopeAsync(int roleId, string expectedScope);
    Task<int> ResolveRoleIdAsync(string roleCode, string expectedScope);
    Task<string?> ResolveRoleCodeAsync(int? roleId, string? legacyRole, string expectedScope);
}
