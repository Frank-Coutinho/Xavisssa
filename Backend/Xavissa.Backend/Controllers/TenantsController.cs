using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Xavissa.Backend.Security;
using Xavissa.Database;
using Xavissa.Database.Models;

namespace Xavissa.Backend.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class TenantsController : ControllerBase
    {
        private readonly XavissaDbContext _db;
        private readonly TenantAccessService _tenantAccess;

        public TenantsController(XavissaDbContext db, TenantAccessService tenantAccess)
        {
            _db = db;
            _tenantAccess = tenantAccess;
        }

        [HttpGet]
        public async Task<IActionResult> GetTenants()
        {
            var query = _db.Tenants.AsQueryable();
            if (!_tenantAccess.IsPlatformAdmin)
                query = query.Where(t => _tenantAccess.AllowedTenantIds.Contains(t.Id));
            return Ok(await query.OrderBy(t => t.Name).ToListAsync());
        }

        [HttpPost]
        [Authorize(Roles = AccessRoles.SystemAdmin)]
        public async Task<IActionResult> CreateTenant([FromBody] Tenant tenant)
        {
            tenant.CreatedBy = _tenantAccess.CurrentUserId;
            _db.Tenants.Add(tenant);
            await _db.SaveChangesAsync();
            return Ok(tenant);
        }

        [HttpPut("{id:int}")]
        public async Task<IActionResult> UpdateTenant(int id, [FromBody] Tenant request)
        {
            var tenant = await _db.Tenants.FirstOrDefaultAsync(t => t.Id == id);
            if (tenant == null)
                return NotFound();
            var permission = _tenantAccess.EnsureTenantManagement(id);
            if (permission != null)
                return permission;

            tenant.Name = request.Name;
            tenant.Code = request.Code;
            tenant.IsActive = request.IsActive;
            tenant.UpdatedAt = DateTime.UtcNow;
            tenant.UpdatedBy = _tenantAccess.CurrentUserId;
            await _db.SaveChangesAsync();
            return Ok(tenant);
        }
    }
}
