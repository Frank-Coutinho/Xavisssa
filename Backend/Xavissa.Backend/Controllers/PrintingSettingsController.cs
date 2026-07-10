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
    public class PrintingSettingsController : ControllerBase
    {
        private readonly XavissaDbContext _db;
        private readonly TenantAccessService _tenantAccess;

        public PrintingSettingsController(XavissaDbContext db, TenantAccessService tenantAccess)
        {
            _db = db;
            _tenantAccess = tenantAccess;
        }

        [HttpGet("tenant/{tenantId:int}")]
        public async Task<IActionResult> GetTenantSettings(int tenantId)
        {
            var permission = _tenantAccess.EnsureTenantAccess(tenantId);
            if (permission != null)
                return permission;
            var settings = await _db.TenantPrintingSettings.FirstOrDefaultAsync(x => x.TenantId == tenantId);
            return Ok(settings);
        }

        [HttpPut("tenant/{tenantId:int}")]
        public async Task<IActionResult> UpsertTenantSettings(int tenantId, [FromBody] TenantPrintingSetting request)
        {
            var permission = _tenantAccess.EnsureTenantManagement(tenantId);
            if (permission != null)
                return permission;
            var settings = await _db.TenantPrintingSettings.FirstOrDefaultAsync(x => x.TenantId == tenantId);
            if (settings == null)
            {
                request.TenantId = tenantId;
                request.UpdatedBy = _tenantAccess.CurrentUserId;
                _db.TenantPrintingSettings.Add(request);
                await _db.SaveChangesAsync();
                return Ok(request);
            }

            settings.ReceiptHeader = request.ReceiptHeader;
            settings.ReceiptFooter = request.ReceiptFooter;
            settings.PaperWidth = request.PaperWidth;
            settings.ShowLogo = request.ShowLogo;
            settings.PrinterName = request.PrinterName;
            settings.BarcodeLabelTemplate = request.BarcodeLabelTemplate;
            settings.UpdatedAt = DateTime.UtcNow;
            settings.UpdatedBy = _tenantAccess.CurrentUserId;
            await _db.SaveChangesAsync();
            return Ok(settings);
        }

        [HttpGet("store/{storeId:int}")]
        public async Task<IActionResult> GetStoreSettings(int storeId)
        {
            var permission = _tenantAccess.EnsureStoreManagement(storeId);
            if (permission != null)
                return permission;
            var settings = await _db.StorePrintingSettings.FirstOrDefaultAsync(x => x.StoreId == storeId);
            return Ok(settings);
        }

        [HttpPut("store/{storeId:int}")]
        public async Task<IActionResult> UpsertStoreSettings(int storeId, [FromBody] StorePrintingSetting request)
        {
            var permission = _tenantAccess.EnsureStoreManagement(storeId);
            if (permission != null)
                return permission;
            var settings = await _db.StorePrintingSettings.FirstOrDefaultAsync(x => x.StoreId == storeId);
            if (settings == null)
            {
                request.StoreId = storeId;
                request.UpdatedBy = _tenantAccess.CurrentUserId;
                _db.StorePrintingSettings.Add(request);
                await _db.SaveChangesAsync();
                return Ok(request);
            }

            settings.ReceiptHeaderOverride = request.ReceiptHeaderOverride;
            settings.ReceiptFooterOverride = request.ReceiptFooterOverride;
            settings.PrinterName = request.PrinterName;
            settings.BarcodeLabelTemplate = request.BarcodeLabelTemplate;
            settings.UpdatedAt = DateTime.UtcNow;
            settings.UpdatedBy = _tenantAccess.CurrentUserId;
            await _db.SaveChangesAsync();
            return Ok(settings);
        }
    }
}
