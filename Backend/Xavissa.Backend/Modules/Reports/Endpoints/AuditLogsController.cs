using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Xavissa.Backend.Security;
using Xavissa.Database;

namespace Xavissa.Backend.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class AuditLogsController : ControllerBase
    {
        private readonly XavissaDbContext _db;
        private readonly TenantAccessService _tenantAccess;

        public AuditLogsController(XavissaDbContext db, TenantAccessService tenantAccess)
        {
            _db = db;
            _tenantAccess = tenantAccess;
        }

        [HttpGet]
        public async Task<IActionResult> GetLogs([FromQuery] int? tenantId = null, [FromQuery] int? storeId = null)
        {
            var query = _db.AuditLogs.AsQueryable();
            if (tenantId.HasValue)
            {
                var permission = _tenantAccess.EnsureTenantAccess(tenantId.Value);
                if (permission != null)
                    return permission;
                query = query.Where(x => x.TenantId == tenantId.Value);
            }
            if (storeId.HasValue)
            {
                if (!_tenantAccess.CanAccessStore(storeId.Value))
                    return Forbid();
                query = query.Where(x => x.StoreId == storeId.Value);
            }
            return Ok(await query.OrderByDescending(x => x.CreatedAt).Take(500).ToListAsync());
        }
    }
}
