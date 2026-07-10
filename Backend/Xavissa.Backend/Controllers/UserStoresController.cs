using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Xavissa.Backend.Security;
using Xavissa.Backend.Services;
using Xavissa.Database;
using Xavissa.Database.Models;

namespace Xavissa.Backend.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class UserStoresController : ControllerBase
    {
        private readonly XavissaDbContext _db;
        private readonly TenantAccessService _tenantAccess;
        private readonly IRoleService _roleService;

        public UserStoresController(XavissaDbContext db, TenantAccessService tenantAccess, IRoleService roleService)
        {
            _db = db;
            _tenantAccess = tenantAccess;
            _roleService = roleService;
        }

        [HttpPost]
        public async Task<ActionResult<UserStoreAssignmentResponse>> AssignStore(
            [FromBody] AssignUserStoreRequest request
        )
        {
            var requestedRoleCode = request.RoleCode ?? request.Role;
            int roleId;
            try
            {
                roleId = request.RoleId ?? await _roleService.ResolveRoleIdAsync(requestedRoleCode ?? AccessRoles.Clerk, RoleScopes.Store);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(ex.Message);
            }

            var normalizedRole = (await _db.Roles.AsNoTracking().FirstAsync(x => x.Id == roleId)).Code;
            var store = await _db.Stores.FirstOrDefaultAsync(s => s.Id == request.StoreId);
            if (store == null)
                return NotFound("Store not found.");
            if (store.TenantId != request.TenantId)
                return BadRequest("Store does not belong to the requested tenant.");

            ActionResult? permission;
            if (_tenantAccess.IsSupport || _tenantAccess.ActingRole.IsTenantAdmin())
            {
                permission = _tenantAccess.EnsureTenantManagement(request.TenantId);
            }
            else if (_tenantAccess.ActingRole.IsStoreManager())
            {
                if (normalizedRole != AccessRoles.Clerk)
                    return Forbid();

                permission = _tenantAccess.EnsureStoreManagement(request.StoreId);
            }
            else
            {
                return Forbid();
            }

            if (permission != null)
                return permission;

            var matchingAssignments = await _db
                .UserStoreRoles.Include(us => us.RoleNavigation)
                .Where(us => us.UserId == request.UserId && us.StoreId == request.StoreId)
                .OrderBy(us => us.Id)
                .ToListAsync();

            var assignment = matchingAssignments.FirstOrDefault();
            if (assignment == null)
            {
                assignment = new UserStoreRole
                {
                    UserId = request.UserId,
                    StoreId = request.StoreId,
                    TenantId = request.TenantId,
                    RoleId = roleId,
                    IsActive = true,
                    CreatedBy = _tenantAccess.CurrentUserId,
                };
                _db.UserStoreRoles.Add(assignment);
            }
            else
            {
                var currentAssignmentRole = assignment.RoleNavigation?.Code ?? assignment.Role.NormalizeRoleCode();
                if (_tenantAccess.ActingRole.IsStoreManager() && currentAssignmentRole.IsStoreManager())
                    return Forbid();

                assignment.RoleId = roleId;
                assignment.TenantId = request.TenantId;
                assignment.IsActive = true;
                assignment.UpdatedBy = _tenantAccess.CurrentUserId;
                assignment.UpdatedAt = DateTime.UtcNow;

                if (matchingAssignments.Count > 1)
                {
                    _db.UserStoreRoles.RemoveRange(matchingAssignments.Skip(1));
                }
            }

            await _db.SaveChangesAsync();
            return Ok(
                new UserStoreAssignmentResponse
                {
                    UserId = assignment.UserId,
                    StoreId = assignment.StoreId,
                    TenantId = assignment.TenantId ?? 0,
                    StoreRoleId = assignment.RoleId,
                    StoreRoleCode = normalizedRole,
                    Role = normalizedRole,
                }
            );
        }

        [HttpDelete]
        public async Task<ActionResult> RemoveStoreAssignment(
            [FromQuery] int userId,
            [FromQuery] int storeId
        )
        {
            var assignment = await _db.UserStoreRoles
                .Include(us => us.RoleNavigation)
                .FirstOrDefaultAsync(us => us.UserId == userId && us.StoreId == storeId);
            if (assignment == null)
                return NotFound();
            if (!assignment.TenantId.HasValue)
                return BadRequest("Tenant is missing on assignment.");

            ActionResult? permission;
            if (_tenantAccess.IsSupport || _tenantAccess.ActingRole.IsTenantAdmin())
            {
                permission = _tenantAccess.EnsureTenantManagement(assignment.TenantId.Value);
            }
            else if (_tenantAccess.ActingRole.IsStoreManager())
            {
                var roleCode = assignment.RoleNavigation?.Code ?? assignment.Role.NormalizeRoleCode();
                if (roleCode.IsStoreManager())
                    return Forbid();

                permission = _tenantAccess.EnsureStoreManagement(storeId);
            }
            else
            {
                return Forbid();
            }

            if (permission != null)
                return permission;

            _db.UserStoreRoles.Remove(assignment);
            await _db.SaveChangesAsync();
            return NoContent();
        }

        [HttpGet("{userId:int}")]
        public async Task<ActionResult<IEnumerable<object>>> GetUserStores(int userId)
        {
            var assignments = await _db
                .UserStoreRoles.Include(us => us.Store)
                .Include(us => us.RoleNavigation)
                .Where(us => us.UserId == userId)
                .OrderBy(us => us.Id)
                .ToListAsync();

            var distinctAssignments = assignments
                .Select(us => new
                {
                    Assignment = us,
                    NormalizedRole = us.RoleNavigation?.Code ?? us.Role.NormalizeRoleCode(),
                })
                .GroupBy(item => new
                {
                    item.Assignment.StoreId,
                    item.NormalizedRole,
                })
                .Select(group => group
                    .OrderByDescending(item => item.Assignment.IsActive)
                    .ThenBy(item => item.Assignment.Id)
                    .First())
                .Select(item => new
                {
                    item.Assignment.UserId,
                    item.Assignment.StoreId,
                    item.Assignment.TenantId,
                    StoreName = item.Assignment.Store.Name,
                    StoreRoleId = item.Assignment.RoleId,
                    StoreRoleCode = item.NormalizedRole,
                    Role = item.NormalizedRole,
                    item.Assignment.IsActive,
                })
                .OrderBy(item => item.StoreName)
                .ThenBy(item => item.Role)
                .ToList();
            return Ok(distinctAssignments);
        }

        public class AssignUserStoreRequest
        {
            public int UserId { get; set; }
            public int TenantId { get; set; }
            public int StoreId { get; set; }
            public int? RoleId { get; set; }
            public string? RoleCode { get; set; }
            public string? Role { get; set; }
        }

        public class UserStoreAssignmentResponse
        {
            public int UserId { get; set; }
            public int TenantId { get; set; }
            public int StoreId { get; set; }
            public int? StoreRoleId { get; set; }
            public string StoreRoleCode { get; set; } = string.Empty;
            public string Role { get; set; } = string.Empty;
        }
    }
}

