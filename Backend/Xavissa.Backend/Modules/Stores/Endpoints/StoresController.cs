using System.Text;
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
    [Route("api/v1/stores")]
    [Authorize]
    public class StoresController : ControllerBase
    {
        private readonly XavissaDbContext _db;
        private readonly TenantAccessService _tenantAccess;

        public StoresController(
            XavissaDbContext db,
            TenantAccessService tenantAccess)
        {
            _db = db;
            _tenantAccess = tenantAccess;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<Store>>> GetStores([FromQuery] int? tenantId = null)
        {
            var query = _db.Stores.AsQueryable();

            if (tenantId.HasValue)
            {
                var permission = _tenantAccess.EnsureTenantAccess(tenantId.Value);
                if (permission != null)
                    return permission;

                query = query.Where(s => s.TenantId == tenantId.Value);
            }

            if (!_tenantAccess.IsPlatformAdmin)
            {
                if (_tenantAccess.IsSupport || _tenantAccess.ActingRole.IsTenantAdmin())
                {
                    query = query.Where(s => s.TenantId.HasValue && _tenantAccess.AllowedTenantIds.Contains(s.TenantId.Value));
                }
                else
                {
                    query = query.Where(s => _tenantAccess.AllowedStoreIds.Contains(s.Id));
                }
            }

            return Ok(await query.OrderBy(s => s.Name).ToListAsync());
        }

        [HttpPost]
        public async Task<ActionResult<Store>> CreateStore([FromBody] Store request)
        {
            if (!request.TenantId.HasValue)
                return BadRequest("TenantId is required.");
            var permission = _tenantAccess.EnsureTenantManagement(request.TenantId.Value);
            if (permission != null)
                return permission;

            var store = new Store
            {
                TenantId = request.TenantId,
                Name = request.Name.Trim(),
                Code = await GenerateStoreCodeAsync(request.Name, request.TenantId.Value),
                IsActive = request.IsActive,
                UpdatedBy = _tenantAccess.CurrentUserId,
            };

            _db.Stores.Add(store);
            await _db.SaveChangesAsync();

            var settingExists = await _db.StoreOperationalSettings
                .IgnoreQueryFilters()
                .AnyAsync(x => x.StoreId == store.Id);
            if (!settingExists)
            {
                _db.StoreOperationalSettings.Add(new StoreOperationalSetting
                {
                    TenantId = store.TenantId,
                    StoreId = store.Id,
                    CashRegisterMode = CashRegisterModes.Disabled,
                    CreatedBy = _tenantAccess.CurrentUserId,
                    UpdatedBy = _tenantAccess.CurrentUserId,
                });
                await _db.SaveChangesAsync();
            }

            return CreatedAtAction(nameof(GetStores), new { id = store.Id }, store);
        }

        [HttpPut("{id:int}")]
        public async Task<ActionResult<Store>> UpdateStore(int id, [FromBody] Store request)
        {
            var store = await _db.Stores.FirstOrDefaultAsync(s => s.Id == id);
            if (store == null)
                return NotFound();
            if (!store.TenantId.HasValue)
                return BadRequest("Store tenant is missing.");
            var permission = _tenantAccess.EnsureTenantManagement(store.TenantId.Value);
            if (permission != null)
                return permission;

            store.Name = request.Name.Trim();
            store.IsActive = request.IsActive;
            store.Code = string.IsNullOrWhiteSpace(store.Code) ? await GenerateStoreCodeAsync(request.Name, store.TenantId.Value, id) : store.Code;
            store.UpdatedAt = DateTime.UtcNow;
            store.UpdatedBy = _tenantAccess.CurrentUserId;

            await _db.SaveChangesAsync();
            return Ok(store);
        }

        [HttpDelete("{id:int}")]
        public async Task<ActionResult> DeleteStore(int id)
        {
            var store = await _db.Stores.IgnoreQueryFilters().FirstOrDefaultAsync(s => s.Id == id);
            if (store == null)
                return NotFound();
            if (!store.TenantId.HasValue)
                return BadRequest("Store tenant is missing.");
            var permission = _tenantAccess.EnsureTenantManagement(store.TenantId.Value);
            if (permission != null)
                return permission;

            if (await HasStoreBusinessDependenciesAsync(id))
            {
                return Conflict("This store cannot be deleted because it has historical records. Deactivate it instead.");
            }

            var operationalSettings = await _db.StoreOperationalSettings
                .IgnoreQueryFilters()
                .Where(x => x.StoreId == id)
                .ToListAsync();
            var printingSettings = await _db.StorePrintingSettings
                .IgnoreQueryFilters()
                .Where(x => x.StoreId == id)
                .ToListAsync();

            _db.StoreOperationalSettings.RemoveRange(operationalSettings);
            _db.StorePrintingSettings.RemoveRange(printingSettings);
            _db.Stores.Remove(store);

            await _db.SaveChangesAsync();
            return NoContent();
        }

        private async Task<bool> HasStoreBusinessDependenciesAsync(int storeId)
        {
            return await _db.Sales.IgnoreQueryFilters().AnyAsync(x => x.StoreId == storeId)
                || await _db.SaleItems.IgnoreQueryFilters().AnyAsync(x => x.StoreId == storeId)
                || await _db.SalePayments.IgnoreQueryFilters().AnyAsync(x => x.StoreId == storeId)
                || await _db.StockLevels.IgnoreQueryFilters().AnyAsync(x => x.StoreId == storeId)
                || await _db.StockMovements.IgnoreQueryFilters().AnyAsync(x => x.StoreId == storeId)
                || await _db.StockAdjustments.IgnoreQueryFilters().AnyAsync(x => x.StoreId == storeId)
                || await _db.StockTransfers.IgnoreQueryFilters().AnyAsync(x => x.FromStoreId == storeId || x.ToStoreId == storeId)
                || await _db.CashRegisterSessions.IgnoreQueryFilters().AnyAsync(x => x.StoreId == storeId)
                || await _db.CashRegisterCashMovements.IgnoreQueryFilters().AnyAsync(x => x.StoreId == storeId)
                || await _db.ProductStoreAssignments.IgnoreQueryFilters().AnyAsync(x => x.StoreId == storeId)
                || await _db.ProductVariants.IgnoreQueryFilters().AnyAsync(x => x.ProductStoreAssignment != null && x.ProductStoreAssignment.StoreId == storeId)
                || await _db.UserStoreRoles.IgnoreQueryFilters().AnyAsync(x => x.StoreId == storeId)
                || await _db.AuditLogs.IgnoreQueryFilters().AnyAsync(x => x.StoreId == storeId);
        }

        private async Task<string> GenerateStoreCodeAsync(string name, int tenantId, int? excludingStoreId = null)
        {
            var baseCode = BuildCodeStem(name);
            var code = baseCode;
            var suffix = 2;
            while (await _db.Stores.AnyAsync(s => s.TenantId == tenantId && s.Code == code && (!excludingStoreId.HasValue || s.Id != excludingStoreId.Value)))
            {
                code = $"{baseCode}-{suffix}";
                suffix++;
            }
            return code;
        }

        private static string BuildCodeStem(string name)
        {
            var lettersAndDigits = new string(name.Trim().ToUpperInvariant().Select(ch => char.IsLetterOrDigit(ch) ? ch : '-').ToArray());
            var builder = new StringBuilder();
            var previousWasDash = false;
            foreach (var ch in lettersAndDigits)
            {
                if (ch == '-')
                {
                    if (previousWasDash)
                        continue;
                    previousWasDash = true;
                    builder.Append(ch);
                    continue;
                }
                previousWasDash = false;
                builder.Append(ch);
            }
            var normalized = builder.ToString().Trim('-');
            return string.IsNullOrWhiteSpace(normalized) ? "STORE" : normalized;
        }
    }
}
