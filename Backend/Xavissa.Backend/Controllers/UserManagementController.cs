using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Mvc;
using Xavissa.Backend.DTOs;
using Xavissa.Backend.Security;
using Xavissa.Backend.Services;

namespace Xavissa.Backend.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Route("api/v1/users")]
    [Authorize]
    public class UserManagementController : ControllerBase
    {
        private readonly AuthService _authService;
        private readonly TenantAccessService _tenantAccess;
        private readonly ILogger<UserManagementController> _logger;

        public UserManagementController(
            AuthService authService,
            TenantAccessService tenantAccess,
            ILogger<UserManagementController> logger)
        {
            _authService = authService;
            _tenantAccess = tenantAccess;
            _logger = logger;
        }

        [HttpPost]
        public IActionResult CreateUser()
        {
            return BadRequest(
                "Use a role-specific endpoint: create-system-admin, create-support, create-tenant-admin, create-store-manager, or create-clerk."
            );
        }

        [HttpPost("create-system-admin")]
        public Task<IActionResult> CreateSystemAdmin([FromBody] PlatformUserCreateRequest request)
        {
            return Task.FromResult<IActionResult>(
                BadRequest("System admin creation is not available through this application.")
            );
        }

        [HttpPost("create-support")]
        [Authorize(Roles = AccessRoles.SystemAdmin)]
        public Task<IActionResult> CreateSupport([FromBody] PlatformUserCreateRequest request)
        {
            return CreateUserInternal(
                request.Username,
                request.Email,
                request.Password,
                AccessRoles.Support,
                null,
                null,
                null
            );
        }

        [HttpPost("create-tenant-admin")]
        [Authorize(Roles = AccessRoles.SystemAdmin)]
        public Task<IActionResult> CreateTenantAdmin([FromBody] TenantScopedUserCreateRequest request)
        {
            return CreateUserInternal(
                request.Username,
                request.Email,
                request.Password,
                AccessRoles.User,
                AccessRoles.TenantAdmin,
                request.TenantId,
                null
            );
        }

        [HttpPost("create-store-manager")]
        public Task<IActionResult> CreateStoreManager([FromBody] StoreScopedUserCreateRequest request)
        {
            _logger.LogInformation(
                "CreateStoreManager requested by user {CurrentUserId}. Username={Username}, TenantId={TenantId}, StoreId={StoreId}, ActingRole={ActingRole}, SelectedTenantId={SelectedTenantId}, SelectedStoreId={SelectedStoreId}",
                _tenantAccess.CurrentUserId,
                request.Username,
                request.TenantId,
                request.StoreId,
                _tenantAccess.ActingRole,
                _tenantAccess.SelectedTenantId,
                _tenantAccess.SelectedStoreId);

            if (!_tenantAccess.IsSupport && !_tenantAccess.ActingRole.IsTenantAdmin())
                return Task.FromResult<IActionResult>(Forbid());

            return CreateUserInternal(
                request.Username,
                request.Email,
                request.Password,
                AccessRoles.User,
                AccessRoles.StoreManager,
                request.TenantId,
                request.StoreId
            );
        }

        [HttpPost("create-clerk")]
        public Task<IActionResult> CreateClerk([FromBody] StoreScopedUserCreateRequest request)
        {
            _logger.LogInformation(
                "CreateClerk requested by user {CurrentUserId}. Username={Username}, TenantId={TenantId}, StoreId={StoreId}, ActingRole={ActingRole}, SelectedTenantId={SelectedTenantId}, SelectedStoreId={SelectedStoreId}",
                _tenantAccess.CurrentUserId,
                request.Username,
                request.TenantId,
                request.StoreId,
                _tenantAccess.ActingRole,
                _tenantAccess.SelectedTenantId,
                _tenantAccess.SelectedStoreId);

            var canCreateClerk =
                _tenantAccess.IsSupport
                || _tenantAccess.ActingRole.IsTenantAdmin()
                || _tenantAccess.ActingRole.IsStoreManager();
            if (!canCreateClerk)
                return Task.FromResult<IActionResult>(Forbid());

            return CreateUserInternal(
                request.Username,
                request.Email,
                request.Password,
                AccessRoles.User,
                AccessRoles.Clerk,
                request.TenantId,
                request.StoreId
            );
        }

        [HttpPut("{id:int}")]
        public async Task<IActionResult> UpdateUser(int id, [FromBody] UpdateUserRequest request)
        {
            var currentPlatformRole =
                User.FindFirst("platform_role")?.Value ?? User.FindFirst(ClaimTypes.Role)?.Value;
            var result = await _authService.UpdateUserAsync(
                id,
                request,
                _tenantAccess.CurrentUserId,
                currentPlatformRole,
                _tenantAccess.ActingRole,
                _tenantAccess.SelectedTenantId,
                _tenantAccess.SelectedStoreId,
                _tenantAccess.AllowedTenantIds,
                _tenantAccess.AllowedStoreIds);
            return result
                ? Ok("User updated successfully")
                : NotFound("User not found or not allowed.");
        }

        [HttpDelete("{id:int}")]
        public async Task<IActionResult> DeleteUser(int id)
        {
            var currentPlatformRole =
                User.FindFirst("platform_role")?.Value ?? User.FindFirst(ClaimTypes.Role)?.Value;
            var result = await _authService.DeleteUserAsync(
                id,
                _tenantAccess.CurrentUserId,
                currentPlatformRole,
                _tenantAccess.ActingRole,
                _tenantAccess.SelectedTenantId,
                _tenantAccess.SelectedStoreId,
                _tenantAccess.AllowedTenantIds,
                _tenantAccess.AllowedStoreIds);
            return result
                ? Ok("User deleted successfully")
                : NotFound("User not found or not allowed.");
        }

        private async Task<IActionResult> CreateUserInternal(
            string username,
            string email,
            string password,
            string platformRole,
            string? assignedRole,
            int? tenantId,
            int? storeId
        )
        {
            var user = await _authService.Register(
                username,
                email,
                password,
                platformRole,
                assignedRole,
                tenantId,
                storeId,
                _tenantAccess.CurrentUserId,
                _tenantAccess.PlatformRole,
                _tenantAccess.ActingRole,
                _tenantAccess.SelectedTenantId,
                _tenantAccess.SelectedStoreId,
                _tenantAccess.AllowedTenantIds,
                _tenantAccess.AllowedStoreIds
            );

            return user == null
                ? BadRequest("User already exists or request is invalid.")
                : Ok(user);
        }
    }

    public class PlatformUserCreateRequest
    {
        public string Username { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
    }

    public class TenantScopedUserCreateRequest : PlatformUserCreateRequest
    {
        public int TenantId { get; set; }
    }

    public class StoreScopedUserCreateRequest : TenantScopedUserCreateRequest
    {
        public int? StoreId { get; set; }
    }
}
