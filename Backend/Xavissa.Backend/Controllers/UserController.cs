using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Xavissa.Backend.DTOs;
using Xavissa.Backend.Security;
using Xavissa.Backend.Services;

namespace Xavissa.Backend.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class UsersController : ControllerBase
    {
        private readonly AuthService _authService;
        private readonly TenantAccessService _tenantAccess;

        public UsersController(AuthService authService, TenantAccessService tenantAccess)
        {
            _authService = authService;
            _tenantAccess = tenantAccess;
        }

        [HttpGet("me")]
        public IActionResult Me()
        {
            return Ok(
                new
                {
                    Username = User.Identity?.Name,
                    PlatformRole = User.FindFirst("platform_role")?.Value
                        ?? User.FindFirst(ClaimTypes.Role)?.Value,
                    ActingRole = User.FindFirst("acting_role")?.Value,
                    AllClaims = User.Claims.Select(c => new { c.Type, c.Value }),
                }
            );
        }

        [HttpGet("all")]
        public async Task<IActionResult> GetAllUsers()
        {
            return Ok(
                await _authService.GetUsersForManagementAsync(
                    _tenantAccess.CurrentUserId,
                    _tenantAccess.PlatformRole,
                    _tenantAccess.ActingRole,
                    _tenantAccess.SelectedTenantId,
                    _tenantAccess.SelectedStoreId,
                    _tenantAccess.AllowedTenantIds,
                    _tenantAccess.AllowedStoreIds
                )
            );
        }

        [HttpGet("{id:int}")]
        [Authorize(Roles = AccessRoles.SystemAdmin)]
        public async Task<IActionResult> GetUser(int id)
        {
            var user = await _authService.GetUserByIdAsync(id);
            return user == null ? NotFound() : Ok(user);
        }

        [HttpPut("{id:int}")]
        [Authorize(Roles = AccessRoles.SystemAdmin)]
        public async Task<IActionResult> UpdateUser(int id, [FromBody] UpdateUserRequest request)
        {
            var currentPlatformRole =
                User.FindFirst("platform_role")?.Value ?? User.FindFirst(ClaimTypes.Role)?.Value;
            var success = await _authService.UpdateUserAsync(
                id,
                request,
                _tenantAccess.CurrentUserId,
                currentPlatformRole,
                _tenantAccess.ActingRole,
                _tenantAccess.SelectedTenantId,
                _tenantAccess.SelectedStoreId,
                _tenantAccess.AllowedTenantIds,
                _tenantAccess.AllowedStoreIds);
            return success ? Ok("User updated successfully") : Forbid();
        }

        [HttpDelete("{id:int}")]
        [Authorize(Roles = AccessRoles.SystemAdmin)]
        public async Task<IActionResult> DeleteUser(int id)
        {
            var currentPlatformRole =
                User.FindFirst("platform_role")?.Value ?? User.FindFirst(ClaimTypes.Role)?.Value;
            var success = await _authService.DeleteUserAsync(
                id,
                _tenantAccess.CurrentUserId,
                currentPlatformRole,
                _tenantAccess.ActingRole,
                _tenantAccess.SelectedTenantId,
                _tenantAccess.SelectedStoreId,
                _tenantAccess.AllowedTenantIds,
                _tenantAccess.AllowedStoreIds);
            return success ? Ok("User deleted successfully") : Forbid();
        }
    }
}
