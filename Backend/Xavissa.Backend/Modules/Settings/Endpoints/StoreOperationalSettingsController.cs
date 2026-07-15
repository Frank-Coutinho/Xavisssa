using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Xavissa.Backend.DTOs;
using Xavissa.Backend.Security;
using Xavissa.Database;
using Xavissa.Database.Models;

namespace Xavissa.Backend.Controllers;

[ApiController]
[Authorize]
[Route("api/[controller]")]
[Route("api/v1/store-operational-settings")]
public class StoreOperationalSettingsController : ControllerBase
{
    private readonly XavissaDbContext _db;
    private readonly TenantAccessService _tenantAccess;

    public StoreOperationalSettingsController(XavissaDbContext db, TenantAccessService tenantAccess)
    {
        _db = db;
        _tenantAccess = tenantAccess;
    }

    [HttpGet("store/{storeId:int}")]
    public async Task<ActionResult<StoreOperationalSettingsDto>> GetForStore(int storeId)
    {
        if (!_tenantAccess.CanAccessStore(storeId))
            return Forbid();

        var setting = await GetOrCreateAsync(storeId);
        return Ok(Map(setting));
    }

    [HttpPut("store/{storeId:int}")]
    public async Task<ActionResult<StoreOperationalSettingsDto>> UpdateForStore(
        int storeId,
        [FromBody] UpdateStoreOperationalSettingsDto request)
    {
        var store = await _db.Stores.IgnoreQueryFilters().AsNoTracking().FirstOrDefaultAsync(x => x.Id == storeId);
        if (store == null)
            return NotFound();
        if (!store.TenantId.HasValue)
            return BadRequest("Store tenant is missing.");

        var canUpdate =
            _tenantAccess.IsPlatformAdmin
            || _tenantAccess.IsSupport
            || _tenantAccess.CanManageTenant(store.TenantId.Value);
        if (!canUpdate)
            return Forbid();

        var mode = NormalizeMode(request.CashRegisterMode);
        if (mode == null)
            return BadRequest("CashRegisterMode must be Disabled, Optional, or Required.");

        var setting = await GetOrCreateAsync(storeId);
        setting.CashRegisterMode = mode;
        setting.UpdatedAt = DateTime.UtcNow;
        setting.UpdatedBy = _tenantAccess.CurrentUserId;
        await _db.SaveChangesAsync();

        return Ok(Map(setting));
    }

    private async Task<StoreOperationalSetting> GetOrCreateAsync(int storeId)
    {
        var setting = await _db.StoreOperationalSettings
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(x => x.StoreId == storeId);
        if (setting != null)
            return setting;

        var store = await _db.Stores.IgnoreQueryFilters().FirstAsync(x => x.Id == storeId);
        setting = new StoreOperationalSetting
        {
            TenantId = store.TenantId,
            StoreId = store.Id,
            CashRegisterMode = CashRegisterModes.Disabled,
            CreatedBy = _tenantAccess.CurrentUserId,
            UpdatedBy = _tenantAccess.CurrentUserId,
        };
        _db.StoreOperationalSettings.Add(setting);
        await _db.SaveChangesAsync();
        return setting;
    }

    private static string? NormalizeMode(string? mode)
    {
        if (string.Equals(mode, CashRegisterModes.Disabled, StringComparison.OrdinalIgnoreCase))
            return CashRegisterModes.Disabled;
        if (string.Equals(mode, CashRegisterModes.Optional, StringComparison.OrdinalIgnoreCase))
            return CashRegisterModes.Optional;
        if (string.Equals(mode, CashRegisterModes.Required, StringComparison.OrdinalIgnoreCase))
            return CashRegisterModes.Required;
        return null;
    }

    private static StoreOperationalSettingsDto Map(StoreOperationalSetting setting) => new()
    {
        Id = setting.Id,
        TenantId = setting.TenantId,
        StoreId = setting.StoreId,
        CashRegisterMode = setting.CashRegisterMode,
        CreatedAt = setting.CreatedAt,
        UpdatedAt = setting.UpdatedAt,
    };
}
